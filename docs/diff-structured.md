# Consuming structured diffs

Use `Diff.Compute` when you want to inspect hunks and lines in code rather than
render text — to build a custom viewer, count changes, classify lines, apply
patches, or drive a UI. It returns a `DiffResult` you can walk directly.

For plain text/byte patch output, see [diff-text.md](diff-text.md).

## The result shape

`Diff.Compute` returns a `DiffResult` containing an ordered list of `DiffHunk`s.
Each hunk has a header (line ranges and optional function name) and a list of
`DiffLine`s. The nesting is:

```
DiffResult
  └─ Hunks : IReadOnlyList<DiffHunk>
       ├─ OldStart, OldCount, NewStart, NewCount   (the @@ -a,b +c,d @@ header)
       ├─ FunctionName : ReadOnlyMemory<byte>?      (optional @@ ... @@ name, raw bytes)
       └─ Lines : IReadOnlyList<DiffLine>
            ├─ Kind    : DiffLineKind              (Context | Addition | Deletion)
            ├─ OldLine : int                       (1-based; 0 = N/A)
            ├─ NewLine : int                       (1-based; 0 = N/A)
            └─ Content : ReadOnlyMemory<byte>      (borrowed raw line bytes, no sigil)
```

Type definitions:

```csharp
public sealed record DiffResult(IReadOnlyList<DiffHunk> Hunks)
{
    public bool IsEmpty => Hunks.Count == 0;
}

public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    ReadOnlyMemory<byte>? FunctionName,
    IReadOnlyList<DiffLine> Lines);

public readonly record struct DiffLine(
    DiffLineKind Kind,
    int OldLine,
    int NewLine,
    ReadOnlyMemory<byte> Content);

public enum DiffLineKind { Context, Addition, Deletion }
```

## `DiffHunk` fields

| Field | Meaning |
|---|---|
| `OldStart` | 1-based starting line of the hunk in the old buffer. Matches the number after `@@ -` in unified-diff text. When `OldCount == 0` the value is the line before the insertion point (still 1-based). |
| `OldCount` | Number of old-buffer lines covered (context + deletions). `0` means a pure-insertion hunk. |
| `NewStart` | 1-based starting line in the new buffer. After `+` in text. Same `0`-count convention as `OldStart`. |
| `NewCount` | Number of new-buffer lines covered (context + additions). |
| `FunctionName` | The raw bytes following `@@ ... @@` in unified-diff text, undecoded, or `null` when function-name emission is disabled or no matching line was found. Owned by the hunk (a copy). See [diff-options.md](diff-options.md#includefunctionnames). Decode as UTF-8 for display — see the walk below. |
| `Lines` | The context/addition/deletion lines in order. |

## `DiffLine` fields

| Field | Meaning |
|---|---|
| `Kind` | `Context` (in both buffers), `Addition` (only in new), `Deletion` (only in old). Matches the leading ` `/`+`/`-` sigil in text. |
| `OldLine` | 1-based line number in the old buffer, or `0` when not applicable — i.e. for `Addition` lines (they have no old-buffer presence). |
| `NewLine` | 1-based line number in the new buffer, or `0` for `Deletion` lines. |
| `Content` | A borrowed slice of the source buffer containing the raw line bytes **without** the leading `+`/`-`/` ` sigil. Includes the line terminator when present. |

### Reading `Content`

`Content` is a `ReadOnlyMemory<byte>` slice containing the raw line bytes (the trailing `\n`
included when present in the source). To get a string for display:

```csharp
string text = Encoding.UTF8.GetString(line.Content.Span);
```

> **Lifetime note.** `DiffLine.Content` borrows from the caller's input buffer;
> no line bytes are copied. Mutating the source buffer after `Compute` returns
> is observable through the result. `Compute` accepts `ReadOnlyMemory<byte>`
> so the returned slices can retain references to the source memory. Managed
> arrays remain reachable through those slices, but pooled or externally owned
> memory must remain valid while the result is used: do not return it to a pool
> or dispose its owner during that time. Use `line.Content.ToArray()` when you
> need an independent copy of a line.

## Walking the result

A complete walk that renders a unified-diff-style view from a `DiffResult`
(equivalent to what `Diff.UnifiedDiff` produces, minus the `\ No newline`
markers):

```csharp
using System.Text;
using Xdiff;

DiffResult result = Diff.Compute(oldData, newData);

if (result.IsEmpty)
{
    Console.WriteLine("(no changes)");
    return;
}

var sb = new StringBuilder();
foreach (DiffHunk hunk in result.Hunks)
{
    sb.Append($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");
    if (hunk.FunctionName is { } fn)
    {
        sb.Append(' ');
        sb.Append(Encoding.UTF8.GetString(fn.Span)); // display decode; bytes are the parity surface
    }

    sb.Append('\n');
    foreach (DiffLine line in hunk.Lines)
    {
        char sigil = line.Kind switch
        {
            DiffLineKind.Context => ' ',
            DiffLineKind.Addition => '+',
            DiffLineKind.Deletion => '-',
            _ => '?',
        };
        sb.Append(sigil);
        sb.Append(Encoding.UTF8.GetString(line.Content.Span));
    }
}

Console.Write(sb.ToString());
```

### Inspecting line numbers

The `OldLine` / `NewLine` fields let you map a diff line back to its source
location. A concrete example from the test suite:

```csharp
DiffResult result = Diff.Compute("a\nb\n"u8.ToArray(), "a\nb\nc\n"u8.ToArray());

DiffHunk hunk = result.Hunks[0];
// hunk.OldStart == 1, hunk.OldCount == 2
// hunk.NewStart == 1, hunk.NewCount == 3

DiffLine ctx1  = hunk.Lines[0];  // Kind=Context,   OldLine=1, NewLine=1
DiffLine ctx2  = hunk.Lines[1];  // Kind=Context,   OldLine=2, NewLine=2
DiffLine added = hunk.Lines[2];  // Kind=Addition,  OldLine=0, NewLine=3
```

The addition's `OldLine == 0` is the sentinel "this line does not exist in the
old buffer." A deletion's `NewLine == 0` is the symmetric sentinel.

## Empty diff

`DiffResult.IsEmpty` is `true` when the two inputs were identical and no hunks
were emitted. Use it as an early-out:

```csharp
DiffResult result = Diff.Compute(oldData, newData);
if (result.IsEmpty)
{
    return; // nothing to report
}
```

## Counting additions and deletions

A small aggregate over `hunk.Lines` gives you per-buffer change counts:

```csharp
int additions = 0, deletions = 0;
foreach (DiffHunk hunk in result.Hunks)
{
    foreach (DiffLine line in hunk.Lines)
    {
        switch (line.Kind)
        {
            case DiffLineKind.Addition: additions++;   break;
            case DiffLineKind.Deletion: deletions++;   break;
        }
    }
}

Console.WriteLine($"+{additions} -{deletions}");
```

For a more complete stats reporter, see [recipes.md](recipes.md).

## When to use this vs. `Diff.UnifiedDiff`

| You want | Use |
|---|---|
| The patch as text/bytes | `Diff.UnifiedDiff` (see [diff-text.md](diff-text.md)) |
| To inspect line kinds, line numbers, or content programmatically | `Diff.Compute` |
| To extract the function-name annotation | `Diff.Compute` and read `hunk.FunctionName` (bytes; UTF-8-decode for display) |
| To build a custom renderer or patch applier | `Diff.Compute` |
| To count additions/deletions or build diff stats | `Diff.Compute` |

`Diff.Compute` borrows line content from the source buffers without copying
the line bytes. It still allocates structured result objects and collections,
and copies function-name bytes when present. Prefer `UnifiedDiff` when you
only need the text.
