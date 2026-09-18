# Xdiff

A managed C# port of the [xdiff library](https://github.com/libgit2/xdiff), providing structured diffs, unified diff text, and three-way merges. Supports Myers, patience, and histogram diff algorithms.

See [Compatibility and reference testing](https://github.com/MM-YJN/Xdiff/blob/main/docs/overview.md#compatibility-and-reference-testing) for buffer-level scope, output framing, and reference-fixture evidence.

## Installation

```sh
dotnet add package Xdiff
```

## Quick start

```csharp
using Xdiff;

string patch = Diff.UnifiedDiff("hello\nworld\n", "hello\nXdiff\n");
Console.WriteLine(patch);
```

## Configure a unified diff

Choose an algorithm and control how many unchanged lines surround each change.

```csharp
using Xdiff;

string patch = Diff.UnifiedDiff(
    "apple\nbanana\ncherry\n",
    "apple\nblueberry\ncherry\n",
    new DiffOptions
    {
        Algorithm = DiffAlgorithm.Histogram,
        ContextLines = 1,
        IndentHeuristic = true,
    });

Console.Write(patch);
```

Output contains hunk headers and changed lines; file headers (`---` and `+++`) are not added.

```diff
@@ -1,3 +1,3 @@
 apple
-banana
+blueberry
 cherry
```

## Ignore whitespace changes

Use `WhitespaceMode.IgnoreAll` to compare lines without considering whitespace.

```csharp
using Xdiff;

string patch = Diff.UnifiedDiff(
    "int count = 1;\n",
    "int  count=1;   \n",
    new DiffOptions { Whitespace = WhitespaceMode.IgnoreAll });

Console.WriteLine(patch.Length == 0); // True
```

Other modes ignore changes in whitespace amounts, trailing whitespace, or CRLF versus LF line endings. `IgnoreBlankLines` can suppress changes consisting only of blank lines.

## Inspect structured hunks and lines

`Diff.Compute` accepts byte buffers and returns line ranges and individual changes.

```csharp
using System.Text;
using Xdiff;

byte[] oldData = Encoding.UTF8.GetBytes("hello\nworld\n");
byte[] newData = Encoding.UTF8.GetBytes("hello\nXdiff\n");
DiffResult result = Diff.Compute(oldData, newData);

foreach (DiffHunk hunk in result.Hunks)
{
    Console.WriteLine($"Old: {hunk.OldStart},{hunk.OldCount}; New: {hunk.NewStart},{hunk.NewCount}");

    foreach (DiffLine line in hunk.Lines)
    {
        string text = Encoding.UTF8.GetString(line.Content.Span).TrimEnd('\r', '\n');
        Console.WriteLine($"{line.Kind}: old={line.OldLine}, new={line.NewLine}: {text}");
    }
}
```

Line numbers are 1-based; `OldLine` is `0` for additions and `NewLine` is `0` for deletions. `result.IsEmpty` indicates that no hunks were emitted. Line content and function-name annotations borrow from the input buffers (annotations use the old buffer), so keep their memory valid and unmodified while consuming the result. `hunk.FunctionName` is empty when no annotation is available; use `.ToArray()` for an independent copy.

To stream hunks, derive from `Xdiff.Emit.HunkSinkBase` and call
`Diff.Compute(sink, oldData, newData, options)`. The sink receives `BeginHunk`,
`Line`, and `EndHunk` callbacks, with hunk properties available throughout
`EndHunk`. After success or failure, base-class coordinates reset to zero and
the borrowed `Func` memory becomes empty. If a callback throws, no further
callbacks run, no cleanup or retry of `EndHunk` occurs, and the original
exception propagates. Partial callback effects remain; subclasses manage their
own retained state before reuse. Concurrent or recursive use of the same sink
is unsupported. See the [structured-diff guide](https://github.com/MM-YJN/Xdiff/blob/main/docs/diff-structured.md#streaming-hunks-to-a-sink).

## Write output to your own buffer

The byte overloads accept `ReadOnlyMemory<byte>` inputs. Pass an
`IBufferWriter<byte>` first to append output without allocating a returned byte
array:

```csharp
using System.Buffers;
using Xdiff;

var patchWriter = new ArrayBufferWriter<byte>();
Diff.UnifiedDiff(patchWriter, "a\n"u8.ToArray(), "b\n"u8.ToArray());
ReadOnlyMemory<byte> patchBytes = patchWriter.WrittenMemory;

var mergeWriter = new ArrayBufferWriter<byte>();
int conflicts = Merger.Merge(
    mergeWriter,
    "a\n"u8.ToArray(),
    "ours\n"u8.ToArray(),
    "theirs\n"u8.ToArray());
ReadOnlyMemory<byte> mergedBytes = mergeWriter.WrittenMemory;
```

Inputs are borrowed during these synchronous calls; keep them valid and
unmodified until the call returns. You own the writer and its output lifetime.
Existing content is preserved, and Xdiff never clears or disposes the writer.
The diff writer emits nothing for identical inputs; the merge writer emits the
input verbatim. The merge return value counts unresolved conflicts.

## Merge independent edits

Pass the common ancestor first, followed by each edited version. The string overload returns the merged text.

```csharp
using Xdiff;

string merged = Merger.Merge(
    ancestor: "title\nbody\nfooter\n",
    ours:     "new title\nbody\nfooter\n",
    theirs:   "title\nbody\nnew footer\n");

Console.Write(merged); // "new title\nbody\nnew footer\n"
```

## Detect and display merge conflicts

Use the byte overload to get `HasConflicts` and `ConflictCount`. `MergeStyle.Diff3` includes the ancestor in conflict markers, and labels identify each version.

```csharp
using System.Text;
using Xdiff;

MergeResult result = Merger.Merge(
    Encoding.UTF8.GetBytes("color=blue\n"),
    Encoding.UTF8.GetBytes("color=red\n"),
    Encoding.UTF8.GetBytes("color=green\n"),
    new MergeOptions
    {
        Style = MergeStyle.Diff3,
        AncestorLabel = "base",
        OurLabel = "ours",
        TheirLabel = "theirs",
    });

if (result.HasConflicts)
{
    Console.WriteLine($"Unresolved conflicts: {result.ConflictCount}");
}

Console.Write(Encoding.UTF8.GetString(result.Content));
```

```text
Unresolved conflicts: 1
<<<<<<< ours
color=red
||||||| base
color=blue
=======
color=green
>>>>>>> theirs
```

To resolve conflicting regions automatically, set `Favor = MergeFavor.Ours`, `MergeFavor.Theirs`, or `MergeFavor.Union` in `MergeOptions`. These choices preserve non-conflicting edits from both sides; `Union` includes both sides of a conflict. The string overload can also emit conflict markers, but does not return a conflict count.

See the [documentation](https://github.com/MM-YJN/Xdiff/blob/main/docs/README.md) for more examples and the full options reference.

## License and attribution

The C# port is copyright (C) 2026 Yi Jin and licensed under LGPL-2.1-or-later, with applicable upstream terms. The histogram implementation includes code under the Eclipse Distribution License v1.0 (SPDX: BSD-3-Clause). The `PooledByteBufferWriter` helper is licensed under MIT.

The package includes `LICENSE` and `THIRD-PARTY-NOTICES.md`. See the [license](https://github.com/MM-YJN/Xdiff/blob/main/LICENSE) and [third-party notices](https://github.com/MM-YJN/Xdiff/blob/main/THIRD-PARTY-NOTICES.md) for the license text, upstream attribution, and disclaimers.
