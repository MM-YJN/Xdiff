# Test fixture notices and provenance

Paths below are relative to [Fixtures](Fixtures). These notices cover test
resources, separately from the library distribution notices in
[THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## Material from libgit2

The libgit2 resources listed below were verified byte for byte against committed
objects at [libgit2 revision `f7164261c9bc0a7e0ebf767c584e5192810a8b24`](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24).
This is a **verified matching revision**, not a claim about the original import
revision, which is not established by this attribution.

The applicable upstream notice identifies the libgit2 contributors as copyright
holders and specifies GPLv2 with a linking exception. See
[LICENSE.libgit2.txt](LICENSE.libgit2.txt) for the upstream copyright statement,
linking exception, and complete GPLv2 text, copied verbatim from
[COPYING](https://github.com/libgit2/libgit2/blob/f7164261c9bc0a7e0ebf767c584e5192810a8b24/COPYING).
The excerpt ends after the GPLv2 text, before notices for unrelated bundled
components. See also upstream [AUTHORS](https://github.com/libgit2/libgit2/blob/f7164261c9bc0a7e0ebf767c584e5192810a8b24/AUTHORS).

In the table, `{html,javascript,php}` means all three extensions. The first four
rows cover 12 copied files.

| Fixture paths | Upstream source and provenance |
| --- | --- |
| `diff/before/file.{html,javascript,php}` | Copied verbatim from [tests/resources/userdiff/before](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/userdiff/before), same filenames. |
| `diff/after/file.{html,javascript,php}` | Copied verbatim from [tests/resources/userdiff/after](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/userdiff/after), same filenames. |
| `diff/expected/driver/diff.{html,javascript,php}` | Copied verbatim from [tests/resources/userdiff/expected/driver](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/userdiff/expected/driver), same filenames. |
| `diff/expected/nodriver/diff.{html,javascript,php}` | Copied verbatim from [tests/resources/userdiff/expected/nodriver](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/userdiff/expected/nodriver), same filenames. |
| `diff/expected/funcctx/diff.{html,javascript,php}` | Additional function-context expected outputs containing the attributed libgit2 `userdiff` input material. These are not claimed to be verbatim upstream copies; their original generation revision and generator are not established. |
| `merge_resolve/ancestor/{automergeable,conflicting}.txt` | Extracted from `automergeable.txt` and `conflicting.txt` at commit `c607fc30883e335def28cd686b51f6cfa02b06ec` (merge-base of master and branch) in [tests/resources/merge-resolve/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-resolve/.gitted). |
| `merge_resolve/master/{automergeable,conflicting}.txt` | Extracted from `automergeable.txt` and `conflicting.txt` at commit `bd593285fc7fe4ca18ccdbabf027f5d689101452` (master) in [tests/resources/merge-resolve/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-resolve/.gitted). |
| `merge_resolve/branch/{automergeable,conflicting}.txt` | Extracted from `automergeable.txt` and `conflicting.txt` at commit `7cb63eed597130ba4abb87b3e544b85021905520` (branch) in [tests/resources/merge-resolve/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-resolve/.gitted). |
| `merge_whitespace/ancestor_eol.txt` | Extracted from `test.txt` at commit `0aa2acaa63cacc7a99fab0c2ce3d56572911df19` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted). |
| `merge_whitespace/ancestor_change.txt` | Extracted from `test.txt` at commit `1189e10a62aadf2fea8cd018afb52c1980f40b4f` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted). |
| `merge_whitespace/branch_a_eol.txt` | Extracted from `test.txt` at commit `9c5362069759fb37ae036cef6e4b2f95c6c5eaab` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted). |
| `merge_whitespace/branch_b_eol.txt` | Extracted from `test.txt` at commit `bfe4ea5805af22a5b194259bda6f5f634486f891` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted). |
| `merge_whitespace/branch_a_change.txt` | Extracted from `test.txt` at commit `d95182053c31f8aa09df4fa225f4e668c5320b59` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted). |
| `merge_whitespace/branch_b_change.txt` | Extracted from `test.txt` at commit `b2a69114f4897109fedf1aafea363cb2d2557029` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted). |
| `merge_whitespace/expected_eol.txt` | Extracted from blob `ee3c2aac8e03224c323b58ecb1f9eef616745467` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted), as specified by [generate-goldens.sh](generate-goldens.sh). |
| `merge_whitespace/expected_change.txt` | Extracted from blob `a827eab4fd66ab37a6ebcfaa7b7e341abfd55947` in [tests/resources/merge-whitespace/.gitted](https://github.com/libgit2/libgit2/tree/f7164261c9bc0a7e0ebf767c584e5192810a8b24/tests/resources/merge-whitespace/.gitted), as specified by [generate-goldens.sh](generate-goldens.sh). |
| `merge_whitespace/expected_conflict.txt` | Generated from `branch_a_eol.txt` (ours), `ancestor_eol.txt` (base), and `branch_b_eol.txt` (theirs) with `git merge-file -p`, no whitespace flags, and labels `HEAD`, `base`, `branch_b_eol`, as specified by [generate-goldens.sh](generate-goldens.sh). Output verified against these inputs; the original Git version is not established. |

The merge commit and blob IDs belong to the **bundled fixture repositories**,
not libgit2's main history. Their links therefore point to the enclosing resource
directories at the verified libgit2 revision. `ancestor_eol` is the merge-base of
`branch_a_eol` and `branch_b_eol`; `ancestor_change` is the merge-base of
`branch_a_change` and `branch_b_change` (also `master` in the bundled repository).
All extracted inputs and the two expected-output blobs were compared against
objects in those bundled repositories, rather than their working-tree files.

[generate-goldens.sh](generate-goldens.sh) copies the `before`, `after`, and
`nodriver` files and materializes the merge fixtures. It does not generate the
`driver` or `funcctx` expected outputs.

## Project test inputs and reference outputs

These families use project test inputs rather than the copied libgit2 resources
above. Reference-generated outputs are distinguished from their inputs below.

| Fixture paths | Origin and generation |
| --- | --- |
| `diff/before_long/file.txt`, `diff/after_long/file.txt`; `diff/expected/longfunc/diff.txt` | Project test inputs and an expected diff testing function-name truncation. No retained generator or original reference revision is established for this family. |
| `indent_heuristic/*_old.txt`, `*_new.txt`; `*.expected`, `*.expected_noindent` | Hand-authored project input pairs; expected outputs produced by the C xdiff reference driver in [generate-indent-heuristic-goldens.sh](generate-indent-heuristic-goldens.sh), with and without the indentation heuristic. |
| `myers_heuristic/{before,after,expected}.txt`, `myers_mxcost/{before,after,expected}.txt` | Deterministic project inputs and C xdiff reference outputs generated by [generate-myers-goldens.sh](generate-myers-goldens.sh). |
| `split/*_old.txt`, `*_new.txt`; `*.expected` | Project input pairs and C xdiff reference outputs generated by [generate-split-goldens.sh](generate-split-goldens.sh). |
| `coverage/*.old.bin`, `*.new.bin`; `*.expected.bin` | Project regression inputs and byte-exact C xdiff reference outputs generated by [generate-coverage-goldens.sh](generate-coverage-goldens.sh). Some inputs are constructed in the generator and tests rather than stored as fixtures. This generator requires [libgit2/xdiff commit `c46ae8dd20ed6f4fae8fb9a30cad6932c85269fc`](https://github.com/libgit2/xdiff/tree/c46ae8dd20ed6f4fae8fb9a30cad6932c85269fc) and checks that tracked source matches that revision. |

The indentation, Myers, and split generators accept an external xdiff source
checkout without enforcing a commit pin. Their original generation revisions
are not established here; the coverage generator's pin is not attributed to
those families. For xdiff's library notices, see
[THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md) and [LICENSE](../../LICENSE).
