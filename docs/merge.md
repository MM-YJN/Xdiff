# Three-way merge

Use `Merger.Merge` to combine two divergent edits against a common ancestor.
Given the base (ancestor) and the two sides (ours, theirs), it produces a
synthesized buffer with standard conflict markers for any regions both sides
changed.

For conflict styling, auto-resolution, and refinement options see
[merge-options.md](merge-options.md). For buffer-level scope and reference-fixture
evidence, see [Compatibility and reference testing](overview.md#compatibility-and-reference-testing).

## The three inputs

```csharp
public static MergeResult Merge(
    ReadOnlySpan<byte> ancestor,
    ReadOnlySpan<byte> ours,
    ReadOnlySpan<byte> theirs,
    MergeOptions? options = null);

public static string Merge(
    string ancestor,
    string ours,
    string theirs,
    MergeOptions? options = null);
```

| Position | Name | Role |
|---|---|---|
| 1 | `ancestor` | The common base (a.k.a. base / ours~1). What both sides diverged from. |
| 2 | `ours` | Our side of the merge. |
| 3 | `theirs` | Their side of the merge. |

Order is positional — no labels are required for the merge itself. Labels
(when set on `MergeOptions`) only decorate the conflict markers in the output.

## The two overloads

| Overload | Returns | When to use |
|---|---|---|
| `ReadOnlySpan<byte>` | `MergeResult` (with `byte[] Content` + `int ConflictCount`) | Line-based processing of bytes without a UTF-8 round-trip; no automatic binary detection. |
| `string` | `string` (the merged text) | Inputs are text; you want displayable output. Internally UTF-8 encodes, merges, UTF-8 decodes. |

`MergeResult.Content` is a **freshly allocated** `byte[]`. It never aliases the
input spans — the merge synthesizes a new buffer from possibly-conflicting
regions. Pass stack-allocated spans, rented arrays, anything you like; the
result stands on its own.

## Clean merges

A merge is clean when the two sides don't edit the same region. Three cases
cover most real-world clean merges:

### One side changed, the other equals the ancestor (fast path)

If one side is identical to the ancestor, the other side is returned as-is —
no diffing needed. This is the fast path in `Merger.cs:53`.

```csharp
using Xdiff;

MergeResult r1 = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),   // ancestor
    "a\nX\nc\n"u8.ToArray(),   // ours — changed
    "a\nb\nc\n"u8.ToArray());  // theirs == ancestor
// r1.ConflictCount == 0
// r1.Content == "a\nX\nc\n"  (ours wins because theirs is unchanged)

MergeResult r2 = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nb\nc\n"u8.ToArray(),   // ours == ancestor
    "a\nY\nc\n"u8.ToArray());  // theirs — changed
// r2.Content == "a\nY\nc\n"
```

### Both sides changed the same way

If both sides make the identical edit, there is no conflict — the edit appears
once in the output:

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),   // both sides
    "a\nX\nc\n"u8.ToArray());  //  change b -> X
// r.ConflictCount == 0
// r.Content == "a\nX\nc\n"
```

### Non-overlapping changes

If the two sides edit different regions, both edits appear in the output:

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\nd\ne\n"u8.ToArray(),
    "a\nX\nc\nd\ne\n"u8.ToArray(),   // ours edits line 2
    "a\nb\nc\nY\ne\n"u8.ToArray());  // theirs edits line 4
// r.ConflictCount == 0
// r.Content == "a\nX\nc\nY\ne\n"   (both changes applied)
```

## Conflicts

When both sides edit the **same** region differently, the merge produces a
conflict. `MergeResult.ConflictCount` is greater than zero,
`MergeResult.HasConflicts` is `true`, and the `Content` contains standard
conflict markers:

```csharp
using Xdiff;

MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),   // ours:  b -> X
    "a\nY\nc\n"u8.ToArray()); // theirs: b -> Y

// r.ConflictCount == 1
// r.HasConflicts == true
// r.Content as text:
// a
// <<<<<<<
// X
// =======
// Y
// >>>>>>>
// c
```

The default style is `MergeStyle.Merge`: `<<<<<<<` ours `=======` theirs
`>>>>>>>`. For the diff3 / zdiff3 styles that include the ancestor block, see
[merge-options.md](merge-options.md#style).

### Counting conflicts

`ConflictCount` is the number of unresolved conflict regions. With the default
`MergeFavor.Default` it can be any non-negative integer. With any other
`Favor`, every conflict is auto-resolved and `ConflictCount` is `0`.

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\nd\ne\n"u8.ToArray(),
    "a\nX\nc\nY\ne\n"u8.ToArray(),   // two separate edits
    "a\nZ\nc\nW\ne\n"u8.ToArray(),
    new MergeOptions { Level = MergeLevel.Eager });
// r.ConflictCount == 2  (two distinct overlapping regions)
```

For refinement levels that merge adjacent conflicts, see
[merge-options.md](merge-options.md#level).

## Edge cases

### Empty ancestor

`byte[0]` (or `""`) is a valid base. Both sides adding the same content merges
cleanly; different content conflicts:

```csharp
MergeResult clean = Merger.Merge(
    (byte[])[],
    "a\n"u8.ToArray(),
    "a\n"u8.ToArray());
// clean.ConflictCount == 0
// clean.Content == "a\n"

MergeResult conflict = Merger.Merge(
    (byte[])[],
    "a\n"u8.ToArray(),
    "b\n"u8.ToArray());
// conflict.ConflictCount == 1
// conflict.Content as text:
// <<<<<<<
// a
// =======
// b
// >>>>>>>
```

### No trailing newline

Conflict markers don't introduce a spurious trailing newline. If the inputs
lack a trailing `\n`, the merge output respects that:

```csharp
MergeResult r = Merger.Merge(
    "a\nb"u8.ToArray(),   // no trailing newline
    "a\nX"u8.ToArray(),
    "a\nY"u8.ToArray());
// r.ConflictCount == 1
// r.Content as text:
// a
// <<<<<<<
// X
// =======
// Y
// >>>>>>>
// (no trailing newline after >>>>>>>)
```

## Binary detection is your job

Xdiff treats all input bytes as text — it has no built-in binary sniff. If you
are merging content that might be binary (images, compiled artifacts, packed
data), check for a NUL byte in the first 8000 bytes before calling `Merge`:

```csharp
static bool IsBinary(ReadOnlySpan<byte> data)
{
    int scan = Math.Min(data.Length, 8000);
    for (int i = 0; i < scan; i++)
    {
        if (data[i] == 0) return true;
    }
    return false;
}

if (IsBinary(ours) || IsBinary(theirs))
{
    // pick a side or report a binary conflict; do not call Merger.Merge
}
```

See [recipes.md](recipes.md) for a complete binary-safe merge guard.

## Reading the result

```csharp
public readonly record struct MergeResult(byte[] Content, int ConflictCount)
{
    public bool HasConflicts => ConflictCount > 0;
}
```

| Member | Meaning |
|---|---|
| `Content` | Freshly allocated `byte[]` of the merged result. May contain conflict markers when `ConflictCount > 0`. Decode with `Encoding.UTF8.GetString` for display. |
| `ConflictCount` | Number of unresolved conflict regions. Always `0` when `MergeOptions.Favor` is anything other than `MergeFavor.Default`. |
| `HasConflicts` | Convenience: `ConflictCount > 0`. |

```csharp
using System.Text;
using Xdiff;

MergeResult r = Merger.Merge(ancestor, ours, theirs, opts);
string text = Encoding.UTF8.GetString(r.Content);
if (r.HasConflicts)
{
    Console.Error.WriteLine($"merge produced {r.ConflictCount} conflict(s)");
    Console.Write(text);
    Environment.Exit(1);
}
else
{
    File.WriteAllText("merged.txt", text);
}
```

## When to use the string overload

```csharp
string merged = Merger.Merge("a\nb\nc\n", "a\nX\nc\n", "a\nY\nc\n");
// "a\n<<<<<<<\nX\n=======\nY\n>>>>>>>\nc\n"
```

The string overload is a convenience: it UTF-8 encodes the inputs, calls the
byte overload, and UTF-8 decodes the result. Use it when your inputs and output
are text and you don't need the raw `ConflictCount` alongside the string. If
you need `ConflictCount`, use the byte overload and decode `Content` yourself.