#!/usr/bin/env bash
# Regenerates golden fixtures for MyersDiff Split coverage tests.
#
# Builds a C xdiff driver against the standalone xdiff sources, then generates
# hand-crafted input pairs and produces expected unified-diff output.
#
# Usage: ./generate-split-goldens.sh <path-to-xdiff>
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
FIXTURES="$SCRIPT_DIR/Fixtures/split"
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
        else if (strcmp(argv[argi], "-m") == 0) { flags |= XDF_NEED_MINIMAL; argi++; }
        else if (strcmp(argv[argi], "-c") == 0) {
            if (argi + 1 >= argc) { fprintf(stderr, "-c needs N\n"); return 2; }
            ctx = strtol(argv[argi + 1], NULL, 10); argi += 2;
        }
        else if (strcmp(argv[argi], "--") == 0) { argi++; break; }
        else { fprintf(stderr, "unknown flag %s\n", argv[argi]); return 2; }
    }
    if (argi + 2 != argc) {
        fprintf(stderr, "usage: xdiff_driver [-i] [-p] [-g] [-m] [-c N] f1 f2\n");
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

# --- Helper: generate a fixture pair and produce golden -------------------
generate_fixture() {
    local name="$1"     # fixture base name
    local flags="$2"    # driver flags (empty string for none)
    shift 2

    # Read generator script from stdin
    local dir="$FIXTURES"
    local old_file="$dir/${name}_old.txt"
    local new_file="$dir/${name}_new.txt"
    local expected_file="$dir/${name}.expected"

    # Each fixture generator is a bash function that writes to
    # $old_file and $new_file. We source the heredoc that follows.

    # Generate old and new via a python snippet
    python3 -c "$1"

    echo "Generating golden for $name..."
    # Run driver with provided flags
    if [ -n "$flags" ]; then
        $DRIVER $flags -c 3 "$old_file" "$new_file" > "$expected_file"
    else
        $DRIVER -c 3 "$old_file" "$new_file" > "$expected_file"
    fi
    echo "  wrote $old_file, $new_file, $expected_file"
}

# ===========================================================================
# 1. diff_1_single_100: 100 lines, 1 change in the middle
# ===========================================================================
generate_fixture "diff_1_single_100" "" "
lines_old = []
lines_new = []
for i in range(100):
    if i == 49:
        lines_old.append(f'change_this_line_{i}')
        lines_new.append(f'replaced_line_{i}')
    else:
        lines_old.append(f'line_{i}')
        lines_new.append(f'line_{i}')
with open('$FIXTURES/diff_1_single_100_old.txt', 'w') as f:
    f.write('\\n'.join(lines_old) + '\\n')
with open('$FIXTURES/diff_1_single_100_new.txt', 'w') as f:
    f.write('\\n'.join(lines_new) + '\\n')
"

# ===========================================================================
# 2. diff_2_scattered_200: 200 lines, 10 scattered changes
# ===========================================================================
generate_fixture "diff_2_scattered_200" "" "
lines_old = []
lines_new = []
for i in range(200):
    if i % 20 == 0:
        lines_old.append(f'old_specific_{i}')
        lines_new.append(f'new_specific_{i}')
    else:
        lines_old.append(f'common_line_{i}')
        lines_new.append(f'common_line_{i}')
with open('$FIXTURES/diff_2_scattered_200_old.txt', 'w') as f:
    f.write('\\n'.join(lines_old) + '\\n')
with open('$FIXTURES/diff_2_scattered_200_new.txt', 'w') as f:
    f.write('\\n'.join(lines_new) + '\\n')
"

# ===========================================================================
# 3. diff_3_many_300: 300 lines, ~150 edits (interleaved + mismatches)
# ===========================================================================
generate_fixture "diff_3_many_300" "" "
lines_old = []
lines_new = []
for i in range(300):
    # Every 2 lines, change the value
    if i % 2 == 0:
        lines_old.append(f'alpha_line_{i}')
        lines_new.append(f'beta_line_{i}')
    else:
        lines_old.append(f'shared_line_{i}')
        lines_new.append(f'shared_line_{i}')
with open('$FIXTURES/diff_3_many_300_old.txt', 'w') as f:
    f.write('\\n'.join(lines_old) + '\\n')
with open('$FIXTURES/diff_3_many_300_new.txt', 'w') as f:
    f.write('\\n'.join(lines_new) + '\\n')
"

# ===========================================================================
# 4. diff_4_long_snakes_500: 500 lines, change blocks separated by 30-line matches
#    Each matching run > SnakeCnt (20) to trigger gotSnake
# ===========================================================================
generate_fixture "diff_4_long_snakes_500" "" "
lines_old = []
lines_new = []
block = 0
i = 0
while i < 500:
    # Matching block of 30 lines ( > SnakeCnt = 20 )
    for j in range(30):
        if i >= 500: break
        lines_old.append(f'match_block{block}_line_{j}')
        lines_new.append(f'match_block{block}_line_{j}')
        i += 1
    block += 1
    # Change block of 5 lines
    for j in range(5):
        if i >= 500: break
        lines_old.append(f'old_change_block{block}_line_{i}')
        lines_new.append(f'new_change_block{block}_line_{i}')
        i += 1
    block += 1
with open('$FIXTURES/diff_4_long_snakes_500_old.txt', 'w') as f:
    f.write('\\n'.join(lines_old) + '\\n')
with open('$FIXTURES/diff_4_long_snakes_500_new.txt', 'w') as f:
    f.write('\\n'.join(lines_new) + '\\n')
"

# ===========================================================================
# 5. diff_5_no_match_1000: 1000 completely different lines
# ===========================================================================
generate_fixture "diff_5_no_match_1000" "" "
with open('$FIXTURES/diff_5_no_match_1000_old.txt', 'w') as f:
    for i in range(1000):
        f.write(f'old_unique_line_{i:04d}\\n')
with open('$FIXTURES/diff_5_no_match_1000_new.txt', 'w') as f:
    for i in range(1000):
        f.write(f'new_unique_line_{i:04d}\\n')
"

# ===========================================================================
# 6. diff_6_minimal_200: same as diff_2 but with Minimal algorithm flags
# ===========================================================================
generate_fixture "diff_6_minimal_200" "-m" "
lines_old = []
lines_new = []
for i in range(200):
    if i % 20 == 0:
        lines_old.append(f'old_specific_{i}')
        lines_new.append(f'new_specific_{i}')
    else:
        lines_old.append(f'common_line_{i}')
        lines_new.append(f'common_line_{i}')
with open('$FIXTURES/diff_6_minimal_200_old.txt', 'w') as f:
    f.write('\\n'.join(lines_old) + '\\n')
with open('$FIXTURES/diff_6_minimal_200_new.txt', 'w') as f:
    f.write('\\n'.join(lines_new) + '\\n')
"

# ===========================================================================
# 7. diff_7_shuffle_2000: 2000 shared lines, randomly permuted in file2.
#    Forces Myers Split to recurse heavily and hit the Mxcost exhaustion
#    fallback (L308/350/355), the boundary else-branches (L149/158), and
#    the backward early-termination (L235).
# ===========================================================================
generate_fixture "diff_7_shuffle_2000" "" "
import random
random.seed(99)
n = 2000
perm = list(range(n))
random.shuffle(perm)
with open('$FIXTURES/diff_7_shuffle_2000_old.txt', 'w') as f:
    for i in range(n):
        f.write(f'line_{i:04d}\\n')
with open('$FIXTURES/diff_7_shuffle_2000_new.txt', 'w') as f:
    for i in perm:
        f.write(f'line_{i:04d}\\n')
"

# ===========================================================================
# 8. diff_8_shuffle_400_snake: 400 shared lines shuffled, with a 25-line
#    aligned block. Triggers forward+backward gotSnake (L181/230) and the
#    forward early-termination (L187), plus Mxcost-bwd (L355).
# ===========================================================================
generate_fixture "diff_8_shuffle_400_snake" "" "
import random
random.seed(777)
n = 400
perm = list(range(n))
random.shuffle(perm)
perm2 = perm[:]
block = perm2[200:230]
del perm2[200:230]
random.shuffle(perm2)
perm2 = perm2[:200] + block + perm2[200:]
with open('$FIXTURES/diff_8_shuffle_400_snake_old.txt', 'w') as f:
    f.write('\\n'.join(f'line_{i:04d}' for i in perm) + '\\n')
with open('$FIXTURES/diff_8_shuffle_400_snake_new.txt', 'w') as f:
    f.write('\\n'.join(f'line_{i:04d}' for i in perm2) + '\\n')
"

# ===========================================================================
# 9. diff_9_shuffle_700_snake: 700 shared lines, identity in old vs. shuffled
#    in new with a 25-line block placed off-diagonal. Hits forward
#    early-termination (L187), Mxcost-fwd (L350), and Mxcost-bwd (L355).
# ===========================================================================
generate_fixture "diff_9_shuffle_700_snake" "" "
import random
random.seed(777)
n = 700
block_start_val = 500
block_len = 25
block_pos_in_old = 480
block_pos_in_new = 180
old_no_block = [v for v in range(n) if v not in range(block_start_val, block_start_val + block_len)]
old = old_no_block[:block_pos_in_old] + list(range(block_start_val, block_start_val + block_len)) + old_no_block[block_pos_in_old:]
new = list(range(n))
random.shuffle(new)
new_no_block = [v for v in new if v not in range(block_start_val, block_start_val + block_len)]
new = new_no_block[:block_pos_in_new] + list(range(block_start_val, block_start_val + block_len)) + new_no_block[block_pos_in_new:]
with open('$FIXTURES/diff_9_shuffle_700_snake_old.txt', 'w') as f:
    f.write('\\n'.join(f'line_{i:05d}' for i in old) + '\\n')
with open('$FIXTURES/diff_9_shuffle_700_snake_new.txt', 'w') as f:
    f.write('\\n'.join(f'line_{i:05d}' for i in new) + '\\n')
"

# ===========================================================================
# 10. diff_10_minimal_shuffle_2000: same input as diff_7 but with Minimal
#     algorithm. Triggers the needMin continue path (L242) and forward
#     early-termination (L187) that Minimal's full-walk produces.
# ===========================================================================
generate_fixture "diff_10_minimal_shuffle_2000" "-m" "
import random
random.seed(99)
n = 2000
perm = list(range(n))
random.shuffle(perm)
with open('$FIXTURES/diff_10_minimal_shuffle_2000_old.txt', 'w') as f:
    for i in range(n):
        f.write(f'line_{i:04d}\\n')
with open('$FIXTURES/diff_10_minimal_shuffle_2000_new.txt', 'w') as f:
    for i in perm:
        f.write(f'line_{i:04d}\\n')
"

echo ""
echo "All split goldens regenerated in $FIXTURES"
