#!/usr/bin/env bash
# Regenerates the indent-heuristic golden fixtures from a xdiff checkout
# by compiling a standalone C reference driver against xdiff.
#
# Usage: ./generate-indent-heuristic-goldens.sh <path-to-xdiff>
# Requires an explicit path to a standalone xdiff checkout.
#
# Builds a tiny driver (linked against xdiff with a xdiff-symbol shim)
# that calls xdl_diff and writes only the @@ ... @@ body — matching the output
# of the C# XDiff.UnifiedDiff facade.
#
# The input pairs (<name>_old.txt / <name>_new.txt) are hand-authored and live
# alongside the goldens; only the *.expected{,_noindent} files are regenerated.
#
# Idempotent: re-running overwrites in place.

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
FIXTURES="$SCRIPT_DIR/Fixtures/indent_heuristic"
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

static int out_hunk(void *priv, long ob, long on, long nb, long nn,
    const char *func, long funclen)
{
    FILE *out = ((struct out_ctx *)priv)->out;
    (void)func; (void)funclen;
    fprintf(out, "@@ -");
    if (on == 1) fprintf(out, "%ld", ob); else fprintf(out, "%ld,%ld", ob, on);
    fprintf(out, " +");
    if (nn == 1) fprintf(out, "%ld", nb); else fprintf(out, "%ld,%ld", nb, nn);
    fprintf(out, " @@\n");
    return 0;
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
    int argi = 1;
    while (argi < argc && argv[argi][0] == '-') {
        if (strcmp(argv[argi], "-i") == 0) { flags |= XDF_INDENT_HEURISTIC; argi++; }
        else if (strcmp(argv[argi], "-p") == 0) { flags |= XDF_PATIENCE_DIFF; argi++; }
        else if (strcmp(argv[argi], "-g") == 0) { flags |= XDF_HISTOGRAM_DIFF; argi++; }
        else if (strcmp(argv[argi], "-c") == 0) {
            if (argi + 1 >= argc) { fprintf(stderr, "-c needs N\n"); return 2; }
            ctx = strtol(argv[argi + 1], NULL, 10); argi += 2;
        }
        else if (strcmp(argv[argi], "--") == 0) { argi++; break; }
        else { fprintf(stderr, "unknown flag %s\n", argv[argi]); return 2; }
    }
    if (argi + 2 != argc) {
        fprintf(stderr, "usage: xdiff_driver [-i] [-p] [-g] [-c N] f1 f2\n");
        return 2;
    }
    long s1, s2;
    char *b1 = read_file(argv[argi], &s1);
    char *b2 = read_file(argv[argi + 1], &s2);
    mmfile_t mf1 = { b1, s1 }, mf2 = { b2, s2 };
    xpparam_t xpp = { flags, NULL, 0, NULL, 0 };
    xdemitconf_t xecfg = { ctx, 0, 0, NULL, NULL, NULL };
    struct out_ctx oc = { stdout };
    xdemitcb_t ecb = { &oc, out_hunk, out_line };
    int rc = xdl_diff(&mf1, &mf2, &xpp, &xecfg, &ecb);
    free(b1); free(b2);
    return rc;
}
EOF

DRIVER="$WORKDIR/xdiff_driver"
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

# --- regenerate goldens ----------------------------------------------------
# Each case has <name>_old.txt + <name>_new.txt; produce:
#   <name>.expected         (with -i, XDF_INDENT_HEURISTIC)
#   <name>.expected_noindent (without)
shopt -s nullglob
for old in "$FIXTURES"/*_old.txt; do
    name="$(basename "$old" _old.txt)"
    new="$FIXTURES/${name}_new.txt"
    if [[ ! -f "$new" ]]; then
        echo "warning: missing $new, skipping $name" >&2
        continue
    fi
    "$DRIVER" -i -c 3 "$old" "$new" > "$FIXTURES/${name}.expected"
    "$DRIVER"    -c 3 "$old" "$new" > "$FIXTURES/${name}.expected_noindent"
    echo "regenerated $name"
done

echo "Regenerated indent-heuristic goldens at $FIXTURES"