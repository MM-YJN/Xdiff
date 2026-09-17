# Xdiff — overview

Xdiff is a managed C# port of the [standalone xdiff library](https://github.com/libgit2/xdiff).
It computes unified diffs and three-way merges. The whole library is synchronous
and allocation-aware: no `unsafe`, no reflection, no `await`.

## Compatibility and reference testing

Xdiff implements buffer-level diff and three-way merge operations. Unified output
contains hunk headers and line content, without Git's file headers (`diff --git`,
`---`, `+++`) or repository metadata such as paths, modes, and object IDs.
Consumers requiring complete patches must add file framing and verify compatibility
with their target tools.

Xdiff does not load Git configuration, attributes, or language drivers. Comparisons
with Git or the C xdiff reference require matching input buffers, options, and
function callbacks. `DiffOptions.IndentHeuristic` defaults to `false`; merge
operations always disable the indentation heuristic, including conflict refinement.
These settings do not promise equivalence with Git's defaults.

Byte overloads avoid UTF-8 conversion, but still process inputs as lines. There is
no automatic binary detection or Git binary-patch generation.

Selected regression cases compare output against reference fixtures; this is not
exhaustive compatibility coverage. Only the
[coverage-fixture generator](../tests/Xdiff.UnitTests/generate-coverage-goldens.sh)
enforces standalone xdiff commit `c46ae8dd20ed6f4fae8fb9a30cad6932c85269fc`
and checks that tracked source matches it. That pin does not apply to merge
fixtures or other fixture families. See the
[fixture provenance](../tests/Xdiff.UnitTests/FIXTURE-NOTICES.md) for each family's
sources and generation details, including reference revisions that remain unknown.

## What's in the box

Two static facades, both in the `Xdiff` namespace:

| Facade | Job | Entry points |
|---|---|---|
| `Xdiff.Diff` | Two-way diff (old vs. new) | `Diff.Compute`, `Diff.UnifiedDiff` |
| `Xdiff.Merger` | Three-way merge (ancestor / ours / theirs) | `Merger.Merge` |

Everything else is option records, enums, and result types. There is no state
to hold, no handle to dispose, no DI container to wire. You call a method, you
get a result.

## Reference it

Install the `Xdiff` NuGet package. It targets `net10.0`.

```bash
dotnet add package Xdiff
```

```csharp
using Xdiff;

var patch = Diff.UnifiedDiff(oldText, newText);
var merged = Merger.Merge(ancestor, ours, theirs);
```

> Note the naming: the **project** is `Xdiff`, the **namespace** is `Xdiff`,
> and the diff facade class is `Diff`. The fully-qualified type is
> `Xdiff.Diff`. The merge facade class is `Merger` (`Xdiff.Merger`).

## Quickstart: diff

For a human-readable patch string:

```csharp
using Xdiff;

string patch = Diff.UnifiedDiff(
    "a\nb\nc\n",
    "a\nB\nc\n");

Console.WriteLine(patch);
// @@ -1,3 +1,3 @@
//  a
// -b
// +B
//  c
```

For a programmatic walk over hunks and lines:

```csharp
using Xdiff;

DiffResult result = Diff.Compute(
    "a\nb\n"u8.ToArray(),
    "a\nb\nc\n"u8.ToArray());

Console.WriteLine(result.IsEmpty ? "no changes" : $"{result.Hunks.Count} hunk(s)");
foreach (DiffHunk hunk in result.Hunks)
{
    foreach (DiffLine line in hunk.Lines)
    {
        char sigil = line.Kind switch
        {
            DiffLineKind.Context   => ' ',
            DiffLineKind.Addition   => '+',
            DiffLineKind.Deletion   => '-',
            _ => '?',
        };
        Console.WriteLine($"{sigil} {line.NewLine,3} {line.Content.Length} bytes");
    }
}
```

## Quickstart: merge

```csharp
using Xdiff;

MergeResult result = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),   // ancestor (base)
    "a\nX\nc\n"u8.ToArray(),   // ours
    "a\nY\nc\n"u8.ToArray());  // theirs

Console.WriteLine(result.HasConflicts
    ? $"conflicts: {result.ConflictCount}"
    : "clean merge");
Console.WriteLine(System.Text.Encoding.UTF8.GetString(result.Content));
// conflicts: 1
// a
// <<<<<<<
// X
// =======
// Y
// >>>>>>
// c
```

The string overload returns the merged text directly:

```csharp
using Xdiff;

string merged = Merger.Merge("a\nb\nc\n", "a\nX\nc\n", "a\nb\nc\n");
// "a\nX\nc\n"  (clean: ours is the only side that changed)
```

## The mental model: two kinds of output

Every Xdiff API returns one of two kinds of output, and the distinction is the
single most important thing to internalize:

### 1. Freshly allocated — `UnifiedDiff` and `Merger.Merge`

These methods synthesize a brand-new buffer from scratch. The returned `string`
or `byte[]` is independent of the input buffers and can outlive them freely.
Inputs are accepted as `ReadOnlySpan<byte>` (or `string`) because no borrow is
taken.

- `Diff.UnifiedDiff(string, string, …) → string`
- `Diff.UnifiedDiff(ReadOnlySpan<byte>, ReadOnlySpan<byte>, …) → byte[]`
- `Merger.Merge(ROS<byte>, ROS<byte>, ROS<byte>, …) → MergeResult` (with a fresh
  `MergeResult.Content` byte array)
- `Merger.Merge(string, string, string, …) → string`

**No lifetime concerns.** Pass stack-allocated spans, rented arrays, anything
you like. The result stands on its own.

### 2. Structured objects with borrowed line data — `Diff.Compute`

`Diff.Compute` returns a `DiffResult` (a tree of `DiffHunk`s, each containing a
list of `DiffLine`s). The `DiffLine.Content` field is a `ReadOnlyMemory<byte>`
slice that aliases the caller's input buffer without copying the line bytes.
Mutating the source buffer after `Compute` returns is observable through the
result.

`Compute` takes `ReadOnlyMemory<byte>` so the returned slices can retain
references to the source memory. Managed arrays remain reachable through
those slices. Pooled or externally owned memory must remain valid while the
result is used: do not return it to a pool or dispose its owner during that
time. Use `line.Content.ToArray()` when you need an independent copy of a line.

```csharp
using Xdiff;

byte[] oldData = File.ReadAllBytes("old.txt");
byte[] newData = File.ReadAllBytes("new.txt");

DiffResult result = Diff.Compute(oldData, newData);
// Line content borrows from oldData and newData, retaining their backing arrays.
// Keep the arrays unchanged while consuming the result.
```

## Synchronous and self-contained

- **No async.** All methods are synchronous CPU-bound work. There is no file
  IO, no network, no threadpool hop. Call them from anywhere.
- **No process-global state.** Xdiff holds no static mutable state. Every call
  is self-contained.

## Where to next

| If you want to… | Read |
|---|---|
| Render a unified-diff text/byte patch | [diff-text.md](diff-text.md) |
| Walk hunks and lines in code | [diff-structured.md](diff-structured.md) |
| Pick an algorithm, control whitespace, annotate function names | [diff-options.md](diff-options.md) |
| Combine two divergent edits against a common ancestor | [merge.md](merge.md) |
| Tune conflict shape, auto-resolve, refine conflicts, add labels | [merge-options.md](merge-options.md) |
| Solve a concrete end-to-end task | [recipes.md](recipes.md) |
