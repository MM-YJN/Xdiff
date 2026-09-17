#!/usr/bin/env bash
# Regenerates the large-input Myers golden fixtures from a xdiff checkout
# by compiling a standalone C reference driver against xdiff (the same
# driver used by generate-indent-heuristic-goldens.sh).
#
# Usage: ./generate-myers-goldens.sh <path-to-xdiff>
# Requires an explicit path to a standalone xdiff checkout.
#
# The driver is invoked with no algorithm flags (default Myers, no
# XDF_NEED_MINIMAL) so the heuristic best-split path is active, and writes only
# the @@ ... @@ body — matching the output of the C# XDiff.UnifiedDiff facade.
#
# Two cases are generated, each designed to force a different branch of
# MyersDiff.DoDiff that small fixtures cannot reach:
#   myers_heuristic : scattered changes across ~3000 lines so the edit cost
#                     exceeds HeurMinCost (256) with a long snake present,
#                     exercising the heuristic best-split scan.
#   myers_mxcost    : near-disjoint content so the edit cost climbs to Mxcost
#                     without an early midpoint, exercising the cost-cap
#                     fallback (fbest/bbest clamp).
#
# The before/after inputs are generated deterministically by this script
# (fixed seed); only the .expected file is derived from xdiff.
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
FIXTURES="$SCRIPT_DIR/Fixtures"
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
    long ctx = 3;
    int argi = 1;
    while (argi < argc && argv[argi][0] == '-') {
        if (strcmp(argv[argi], "-c") == 0) {
            if (argi + 1 >= argc) { fprintf(stderr, "-c needs N\n"); return 2; }
            ctx = strtol(argv[argi + 1], NULL, 10); argi += 2;
        }
        else if (strcmp(argv[argi], "--") == 0) { argi++; break; }
        else { fprintf(stderr, "unknown flag %s\n", argv[argi]); return 2; }
    }
    if (argi + 2 != argc) {
        fprintf(stderr, "usage: xdiff_driver [-c N] f1 f2\n");
        return 2;
    }
    long s1, s2;
    char *b1 = read_file(argv[argi], &s1);
    char *b2 = read_file(argv[argi + 1], &s2);
    mmfile_t mf1 = { b1, s1 }, mf2 = { b2, s2 };
    /* No XDF_NEED_MINIMAL: Myers runs with the heuristic best-split path active. */
    xpparam_t xpp = { 0, NULL, 0, NULL, 0 };
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

# --- generate deterministic large inputs -----------------------------------
mkdir -p "$FIXTURES/myers_heuristic" "$FIXTURES/myers_mxcost"

# myers_heuristic: a ~33k-line file (ndiags > 65536, so Mxcost=512). The 25-line
# snake block lives at file1 position 300 but file2 position 0 — i.e. on
# diagonal d=300, which the forward search only reaches at ec ~= 300. The search
# then extends the block (a >20 snake) at high ec, so gotSnake && ec > 256 fires
# the heuristic best-split scan. The filler lines are independently shuffled so
# no early midpoint is found.
python3 - "$FIXTURES/myers_heuristic" <<'PY'
import sys, os, random
d = sys.argv[1]
N = 33000
random.seed(20240101)
snake = [f"snake_{i:04d}\n" for i in range(25)]
filler = [f"fill_{i:05d}\n" for i in range(N - 25)]
fa = filler[:]; random.shuffle(fa)
fb = filler[:]; random.shuffle(fb)
before = fa[:300] + snake + fa[300:]   # snake block at position 300
after  = snake + fb                     # snake block at position 0
with open(os.path.join(d, "before.txt"), "w", newline="\n") as f:
    f.writelines(before)
with open(os.path.join(d, "after.txt"), "w", newline="\n") as f:
    f.writelines(after)
PY

# myers_mxcost: a full shuffle (no preserved blocks). No long snake, so the
# heuristic is skipped, but ec climbs to Mxcost and trips the cost-cap fallback.
python3 - "$FIXTURES/myers_mxcost" <<'PY'
import sys, os, random
d = sys.argv[1]
N = 3000
random.seed(99070731)
before = [f"line_{i:04d}\n" for i in range(N)]
order = list(range(N))
random.shuffle(order)
after = [f"line_{order[i]:04d}\n" for i in range(N)]
with open(os.path.join(d, "before.txt"), "w", newline="\n") as f:
    f.writelines(before)
with open(os.path.join(d, "after.txt"), "w", newline="\n") as f:
    f.writelines(after)
PY

# --- capture goldens --------------------------------------------------------
"$DRIVER" -c 3 "$FIXTURES/myers_heuristic/before.txt" "$FIXTURES/myers_heuristic/after.txt" \
    > "$FIXTURES/myers_heuristic/expected.txt"
"$DRIVER" -c 3 "$FIXTURES/myers_mxcost/before.txt" "$FIXTURES/myers_mxcost/after.txt" \
    > "$FIXTURES/myers_mxcost/expected.txt"

echo "Regenerated Myers large-input goldens under $FIXTURES/{myers_heuristic,myers_mxcost}"
