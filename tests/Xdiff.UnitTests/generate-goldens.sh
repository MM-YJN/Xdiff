#!/usr/bin/env bash
# Materializes golden fixtures from libgit2 tests/resources.
#
# Usage: ./generate-goldens.sh <path-to-resources>
# Requires userdiff, merge-resolve, and merge-whitespace resources.
# Upstream provenance and licensing: [FIXTURE-NOTICES.md](FIXTURE-NOTICES.md).
#
# Materializes:
#   Fixtures/diff/{before,after,expected/nodriver}/file.{html,javascript,php}
#   Fixtures/merge_resolve/{ancestor,master,branch}/{automergeable,conflicting}.txt
#   Fixtures/merge_whitespace/{ancestor_eol,ancestor_change,branch_a_eol,branch_b_eol,branch_a_change,branch_b_change}.txt
#   Fixtures/merge_whitespace/expected_{eol,change,conflict}.txt
#
# Idempotent: re-running overwrites in place.

set -euo pipefail

FIXTURE_RESOURCES="${1:-}"

if [[ ! -d "$FIXTURE_RESOURCES/userdiff" ||
      ! -d "$FIXTURE_RESOURCES/merge-resolve/.gitted" ||
      ! -d "$FIXTURE_RESOURCES/merge-whitespace/.gitted" ]]; then
    echo "error: required fixture resources not found at $FIXTURE_RESOURCES" >&2
    echo "usage: $0 <path-to-resources>" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FIXTURES="$SCRIPT_DIR/Fixtures"

mkdir -p "$FIXTURES/diff/before" "$FIXTURES/diff/after" "$FIXTURES/diff/expected/nodriver"
mkdir -p "$FIXTURES/merge_resolve/ancestor" "$FIXTURES/merge_resolve/master" "$FIXTURES/merge_resolve/branch"
mkdir -p "$FIXTURES/merge_whitespace"

USERDIFF="$FIXTURE_RESOURCES/userdiff"
cp "$USERDIFF/before/file.html"       "$FIXTURES/diff/before/file.html"
cp "$USERDIFF/before/file.javascript" "$FIXTURES/diff/before/file.javascript"
cp "$USERDIFF/before/file.php"        "$FIXTURES/diff/before/file.php"
cp "$USERDIFF/after/file.html"        "$FIXTURES/diff/after/file.html"
cp "$USERDIFF/after/file.javascript"  "$FIXTURES/diff/after/file.javascript"
cp "$USERDIFF/after/file.php"         "$FIXTURES/diff/after/file.php"
cp "$USERDIFF/expected/nodriver/diff.html"       "$FIXTURES/diff/expected/nodriver/diff.html"
cp "$USERDIFF/expected/nodriver/diff.javascript" "$FIXTURES/diff/expected/nodriver/diff.javascript"
cp "$USERDIFF/expected/nodriver/diff.php"        "$FIXTURES/diff/expected/nodriver/diff.php"

MERGE_RESOLVE="$FIXTURE_RESOURCES/merge-resolve/.gitted"
MERGE_BASE=$(git -C "$MERGE_RESOLVE" merge-base master branch)
git -C "$MERGE_RESOLVE" show "$MERGE_BASE:automergeable.txt" > "$FIXTURES/merge_resolve/ancestor/automergeable.txt"
git -C "$MERGE_RESOLVE" show "$MERGE_BASE:conflicting.txt"  > "$FIXTURES/merge_resolve/ancestor/conflicting.txt"
git -C "$MERGE_RESOLVE" show "master:automergeable.txt"     > "$FIXTURES/merge_resolve/master/automergeable.txt"
git -C "$MERGE_RESOLVE" show "master:conflicting.txt"       > "$FIXTURES/merge_resolve/master/conflicting.txt"
git -C "$MERGE_RESOLVE" show "branch:automergeable.txt"     > "$FIXTURES/merge_resolve/branch/automergeable.txt"
git -C "$MERGE_RESOLVE" show "branch:conflicting.txt"       > "$FIXTURES/merge_resolve/branch/conflicting.txt"

MERGE_WS="$FIXTURE_RESOURCES/merge-whitespace/.gitted"
# EOL scenario ancestor = initial commit (merge-base of branch_a_eol/branch_b_eol).
EOL_BASE=$(git -C "$MERGE_WS" merge-base branch_a_eol branch_b_eol)
# Change scenario ancestor = master (merge-base of branch_a_change/branch_b_change).
CHANGE_BASE=$(git -C "$MERGE_WS" merge-base branch_a_change branch_b_change)

git -C "$MERGE_WS" show "$EOL_BASE:test.txt"        > "$FIXTURES/merge_whitespace/ancestor_eol.txt"
git -C "$MERGE_WS" show "$CHANGE_BASE:test.txt"     > "$FIXTURES/merge_whitespace/ancestor_change.txt"
git -C "$MERGE_WS" show "branch_a_eol:test.txt"     > "$FIXTURES/merge_whitespace/branch_a_eol.txt"
git -C "$MERGE_WS" show "branch_b_eol:test.txt"     > "$FIXTURES/merge_whitespace/branch_b_eol.txt"
git -C "$MERGE_WS" show "branch_a_change:test.txt"  > "$FIXTURES/merge_whitespace/branch_a_change.txt"
git -C "$MERGE_WS" show "branch_b_change:test.txt"  > "$FIXTURES/merge_whitespace/branch_b_change.txt"
git -C "$MERGE_WS" cat-file -p ee3c2aac8e03224c323b58ecb1f9eef616745467 > "$FIXTURES/merge_whitespace/expected_eol.txt"
git -C "$MERGE_WS" cat-file -p a827eab4fd66ab37a6ebcfaa7b7e341abfd55947 > "$FIXTURES/merge_whitespace/expected_change.txt"

# Conflict case: EOL scenario with no whitespace flags.
# Ancestor = initial commit (trailing ws on lines 1/11), ours = branch_a_eol, theirs = branch_b_eol.
# Both sides change lines 1 and 11 differently (content vs trailing-ws) → conflict.
git merge-file -p -L "HEAD" -L "base" -L "branch_b_eol" \
    "$FIXTURES/merge_whitespace/branch_a_eol.txt" \
    "$FIXTURES/merge_whitespace/ancestor_eol.txt" \
    "$FIXTURES/merge_whitespace/branch_b_eol.txt" \
    > "$FIXTURES/merge_whitespace/expected_conflict.txt" || true

echo "Regenerated Phase 7 fixtures at $FIXTURES"
