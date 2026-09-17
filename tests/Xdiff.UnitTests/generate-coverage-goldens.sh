#!/usr/bin/env bash
# Regenerates byte-exact coverage regression fixtures from the standalone xdiff checkout.
#
# Builds a C xdiff driver against the standalone xdiff sources, then generates
# hand-crafted input pairs and produces expected unified-diff output.
#
# Usage: ./generate-coverage-goldens.sh <path-to-xdiff>
# Requires an explicit path to a standalone xdiff checkout.

set -euo pipefail

if [[ $# -ne 1 || -z "$1" ]]; then
    echo "usage: $0 <path-to-xdiff>" >&2
    exit 1
fi

XDIFF="$1"

if [[ ! -f "$XDIFF/xdiff.h" ]]; then
    echo "error: xdiff checkout not found at $XDIFF" >&2
    echo "usage: $0 <path-to-xdiff>" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES="$SCRIPT_DIR/Fixtures/coverage"
mkdir -p "$FIXTURES"

if [[ "$(git -C "$XDIFF" rev-parse HEAD)" != "c46ae8dd20ed6f4fae8fb9a30cad6932c85269fc" ]]; then
    echo "error: golden generation requires xdiff commit c46ae8dd20ed6f4fae8fb9a30cad6932c85269fc" >&2
    exit 1
fi

if ! git -C "$XDIFF" diff --quiet HEAD -- .; then
    echo "error: xdiff must match the pinned source commit" >&2
    exit 1
fi
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

# --- driver ----------------------------------------------------------------
cat > "$WORKDIR/xdiff_driver.c" <<'EOF'
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "xdiff.h"

static char *read_file(const char *path, long *size_out)
{
    FILE *f = fopen(path, "rb");
    if (!f) { fprintf(stderr, "cannot open %s\n", path); exit(2); }
    if (fseek(f, 0, SEEK_END) != 0) { perror("fseek"); exit(2); }
    long sz = ftell(f);
    if (sz < 0) { perror("ftell"); exit(2); }
    if (fseek(f, 0, SEEK_SET) != 0) { perror("fseek"); exit(2); }
    char *buf = malloc(sz > 0 ? (size_t)sz : 1);
    if (!buf) { fprintf(stderr, "oom\n"); exit(2); }
    long got = fread(buf, 1, (size_t)sz, f);
    fclose(f);
    if (got != sz) { fprintf(stderr, "short read on %s\n", path); exit(2); }
    *size_out = sz;
    return buf;
}

struct out_ctx { FILE *out; };

/* Same whole-line matcher contract as DiffOptions.FunctionMatcher. */
static long header_func(const char *rec, long len, char *buf, long sz, void *priv)
{
    if (len <= 0 || rec[0] != *(const char *)priv) return -1;
    if (len > sz) len = sz;
    while (len > 0 && isspace((unsigned char)rec[len - 1])) len--;
    memcpy(buf, rec, (size_t)len);
    return len;
}

static int out_line(void *priv, mmbuffer_t *mb, int n)
{
    FILE *out = ((struct out_ctx *)priv)->out;
    for (int i = 0; i < n; i++) fwrite(mb[i].ptr, 1, (size_t)mb[i].size, out);
    return 0;
}

int main(int argc, char **argv)
{
    unsigned long flags = 0;
    long ctx = 3;
    unsigned long emit_flags = 0;
    char header = 0;
    int argi = 1;
    while (argi < argc && argv[argi][0] == '-') {
        if (strcmp(argv[argi], "-i") == 0) { flags |= XDF_INDENT_HEURISTIC; argi++; }
        else if (strcmp(argv[argi], "-p") == 0) { flags |= XDF_PATIENCE_DIFF; argi++; }
        else if (strcmp(argv[argi], "-g") == 0) { flags |= XDF_HISTOGRAM_DIFF; argi++; }
        else if (strcmp(argv[argi], "-m") == 0) { flags |= XDF_NEED_MINIMAL; argi++; }
        else if (strcmp(argv[argi], "-B") == 0) { flags |= XDF_IGNORE_BLANK_LINES; argi++; }
        else if (strcmp(argv[argi], "-F") == 0) { emit_flags |= XDL_EMIT_FUNCNAMES; argi++; }
        else if (strcmp(argv[argi], "-W") == 0) { emit_flags |= XDL_EMIT_FUNCCONTEXT; argi++; }
        else if (strcmp(argv[argi], "-H") == 0) {
            if (argi + 1 >= argc) return 2;
            header = argv[argi + 1][0]; argi += 2;
        }
        else if (strcmp(argv[argi], "-c") == 0) {
            if (argi + 1 >= argc) { fprintf(stderr, "-c needs N\n"); return 2; }
            ctx = strtol(argv[argi + 1], NULL, 10); argi += 2;
        }
        else if (strcmp(argv[argi], "--") == 0) { argi++; break; }
        else { fprintf(stderr, "unknown flag %s\n", argv[argi]); return 2; }
    }
    if (argi + 2 != argc) {
        fprintf(stderr, "usage: xdiff_driver [-i] [-p] [-g] [-m] [-B] [-F] [-W] [-H CHAR] [-c N] f1 f2\n");
        return 2;
    }
    long s1, s2;
    char *b1 = read_file(argv[argi], &s1);
    char *b2 = read_file(argv[argi + 1], &s2);
    mmfile_t mf1 = { b1, s1 }, mf2 = { b2, s2 };
    xpparam_t xpp = { flags, NULL, 0, NULL, 0 };
    xdemitconf_t xecfg = { ctx, 0, emit_flags, header ? header_func : NULL, &header, NULL };
    struct out_ctx oc = { stdout };
    xdemitcb_t ecb = { &oc, NULL, out_line };
    int rc = xdl_diff(&mf1, &mf2, &xpp, &xecfg, &ecb);
    free(b1); free(b2);
    return rc;
}
EOF

DRIVER="$WORKDIR/xdiff_driver"
echo "Compiling C xdiff driver..."
gcc -O2 -std=c11 -Wno-sign-compare -Wno-unused-parameter -Wno-unused-function \
    -I "$XDIFF" \
    "$XDIFF"/xdiffi.c \
    "$XDIFF"/xemit.c \
    "$XDIFF"/xhistogram.c \
    "$XDIFF"/xpatience.c \
    "$XDIFF"/xprepare.c \
    "$XDIFF"/xutils.c \
    "$XDIFF"/xmerge.c \
    "$WORKDIR/xdiff_driver.c" \
    -o "$DRIVER"
echo "Compiled."

# Keep the large Myers inputs deterministic and generate them in the test too;
# only their small emitted diffs are embedded. All other inputs are raw .bin
# fixtures so git's newline conversion cannot alter CRLF or non-UTF-8 bytes.
python3 - "$DRIVER" "$FIXTURES" "$WORKDIR" <<'PY'
from pathlib import Path
import subprocess
import sys

driver, fixtures, work = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])


def golden(name, old, new, flags=(), save_inputs=True):
    (work / "old").write_bytes(old)
    (work / "new").write_bytes(new)
    result = subprocess.run(
        [driver, *flags, str(work / "old"), str(work / "new")],
        check=True, stdout=subprocess.PIPE)
    (fixtures / f"{name}.expected.bin").write_bytes(result.stdout)
    if save_inputs:
        (fixtures / f"{name}.old.bin").write_bytes(old)
        (fixtures / f"{name}.new.bin").write_bytes(new)
    print(f"Generated {name}: {len(result.stdout)} output bytes")


# 68,003 diagonals make BogoSqrt = 512, beyond the 256 heuristic threshold.
# Reversing 300 records at one end and 600 at the other exposes the long
# interior matching run from the cheaper direction first (forward/backward).
for name, front, back in [("myers_forward", 300, 600), ("myers_backward", 600, 300)]:
    old = list(range(34000))
    new = old[:front][::-1] + old[front:34000-back] + old[34000-back:][::-1]
    encode = lambda lines: b"".join(f"line_{i:05d}\n".encode("ascii") for i in lines)
    golden(name, encode(old), encode(new), save_inputs=False)

# Existing function-context smoke assertions now have complete C expectations.
golden("function_smoke",
       b"def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n",
       b"def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n",
       ["-W"], save_inputs=False)
golden("function_custom",
       b"preamble not a section\nsection one\n    a = 1\n    b = 2\nsection two\n    c = 3\n    d = 4\n",
       b"preamble not a section\nsection one\n    a = 1\n    b = 9\nsection two\n    c = 3\n    d = 4\n",
       ["-W", "-F", "-H", "s", "-c", "0"], save_inputs=False)
golden("function_blank",
       b"preamble\nheader alpha\n\n    body1 = 1\n    body2 = 2\n\nheader beta\n    body3 = 3\n",
       b"preamble\nheader alpha\n\n    body1 = 1\n    body2 = 9\n\nheader beta\n    body3 = 3\n",
       ["-W", "-F", "-H", "h", "-c", "0"], save_inputs=False)
golden("function_append",
       b"header alpha\n    body1 = 1\n    body2 = 2\n",
       b"header alpha\n    body1 = 1\n    body2 = 2\nheader beta\n    body3 = 3\n",
       ["-W", "-F", "-H", "h", "-c", "0"], save_inputs=False)

function_cases = {
    "function_multiple_changes": (
        b"# alpha\n  a\n  b\n  c\n  d\n  e\n\n# beta\n  f\n  g\n",
        b"# alpha\n  A\n  b\n  c\n  d\n  E\n\n# beta\n  f\n  G\n"),
    "function_eof_without_header": (
        b"# alpha\n  a\n  b\n",
        b"# alpha\n  a\n  b\n  appended\n  last"),
    "function_no_header": (
        b"  a\n  b\n  c\n", b"  a\n  B\n  c\n"),
    "function_append_after_body": (
        b"# alpha\n  a\n", b"# alpha\n  a\n  continuation\n# beta\n  b\n"),
    "function_raw_bytes": (
        b"# \xff alpha\r\n  a\r\n  b\r\n  \x80tail",
        b"# \xff alpha\r\n  A\r\n  b\r\n  \xfetail"),
    # Ignorable leading blank change is initially skipped by GetHunk, then
    # function expansion must recover it via the previous-change pointer.
    "function_skip_then_recover_blank": (
        b"\npreamble\n\n# alpha\n\n  a\n  b\n  c\n  d\n",
        b"preamble\n\n# alpha\n  a\n  b\n  c\n  D\n"),
    "function_recover_blank": (
        b"# alpha\n\n  a\n  b\n  c\n  d\n",
        b"# alpha\n  a\n  b\n  c\n  D\n"),
}
for name, (old, new) in function_cases.items():
    flags = ["-W", "-F", "-H", "#", "-c", "0"]
    if name in ("function_recover_blank", "function_skip_then_recover_blank"):
        flags.append("-B")
    golden(name, old, new, flags)

# Mixed unmatched / frequent / unique common records exercise both scans and
# both decisions of xdl_clean_mmatch. Separate runs permit compaction to slide
# repeated changes, merge adjacent groups, and choose indentation boundaries.
repeated_cases = {
    "repeated_cleanup": (
        b"old\n" * 40 + b"repeat\n" * 40 + b"old\n" * 40 + b"anchor\n" +
        b"old\n" * 20 + b"repeat\n" * 40 + b"anchor2\n" + b"repeat\n" * 40 + b"old\n" * 20,
        b"new\n" * 30 + b"repeat\n" * 50 + b"new\n" * 30 + b"anchor\n" +
        b"new\n" * 30 + b"repeat\n" * 30 + b"anchor2\n" + b"repeat\n" * 30 + b"new\n" * 30),
    "repeated_discard": (
        b"old\n" * 50 + b"repeat\n" + b"old\n" * 50 + b"anchor\n" + b"repeat\n" * 80 + b"old\n",
        b"new\n" * 50 + b"repeat\n" + b"new\n" * 50 + b"anchor\n" + b"repeat\n" * 80 + b"new\n"),
    # Histogram initially separates the inserted 'a' and repeated 'x';
    # sliding the latter upward joins it to the preceding change group.
    "repeated_slide_up": (b"  x\n", b"a\n  x\n  x\n"),
    "repeated_slide": (
        b"a\na\nb\na\na\nb\na\nb\na\n",
        b"a\nb\na\nb\na\na\nb\na\na\n"),
    "repeated_raw_bytes": (
        b"# \xff\r\n  \x80\r\n  \x80\r\n\r\n  \xfe\r\n  \x80\r\n  tail",
        b"# \xff\r\n  \x80\r\n\r\n  \xfe\r\n  \x80\r\n  \x80\r\n  changed"),
}
for name, (old, new) in repeated_cases.items():
    for alg, alg_flags in [("myers", []), ("minimal", ["-m"]),
                           ("patience", ["-p"]), ("histogram", ["-g"])]:
        for indent in [False, True]:
            variant = f"{name}_{alg}" + ("_indent" if indent else "")
            golden(variant, old, new, alg_flags + (["-i"] if indent else []), save_inputs=False)
    (fixtures / f"{name}.old.bin").write_bytes(old)
    (fixtures / f"{name}.new.bin").write_bytes(new)
PY
