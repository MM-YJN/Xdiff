# Rendering unified-diff text

Use `Diff.UnifiedDiff` to render unified-diff hunks as a `string`, `byte[]`, or
bytes appended to a caller-supplied writer for display, logging, or storage.
For programmatic inspection of hunks and lines, see
[diff-structured.md](diff-structured.md).

## Overloads

```csharp
// UTF-8 string in, UTF-8 string out
public static string UnifiedDiff(string oldText, string newText, DiffOptions? options = null);

// raw bytes in, raw bytes out
public static byte[] UnifiedDiff(ReadOnlyMemory<byte> oldData, ReadOnlyMemory<byte> newData, DiffOptions? options = null);

// raw bytes in, append raw bytes to a caller-owned writer
public static void UnifiedDiff(IBufferWriter<byte> writer, ReadOnlyMemory<byte> oldData, ReadOnlyMemory<byte> newData, DiffOptions? options = null);
```

| Overload | Use when |
|---|---|
| `string` | Inputs are text; you want displayable output. The inputs are UTF-8 encoded internally, the output is UTF-8 decoded. |
| `byte[]` | Inputs are bytes, possibly not valid UTF-8, and you want to avoid UTF-8 conversion. Processing remains line-based. |
| `IBufferWriter<byte>` | Append output to your own buffer without allocating a returned patch array. |

The result-returning overloads return an empty `string` / empty `byte[]` when
the inputs are identical. The writer overload writes nothing in that case.

Output contains `@@` hunk headers and prefixed line content, without Git's file
headers or repository metadata. Consumers requiring complete patches must add
file framing and verify compatibility with their target tools. See
[Compatibility and reference testing](overview.md#compatibility-and-reference-testing)
for the scope of the implementation and reference fixtures.

## Your first diff

```csharp
using Xdiff;

string insert = Diff.UnifiedDiff("a\nb\n", "a\nb\nc\n");
// @@ -1,2 +1,3 @@
//  a
//  b
// +c

string delete = Diff.UnifiedDiff("a\nb\nc\n", "a\nc\n");
// @@ -1,3 +1,2 @@
//  a
// -b
//  c

string replace = Diff.UnifiedDiff("a\nb\nc\n", "a\nB\nc\n");
// @@ -1,3 +1,3 @@
//  a
// -b
// +B
//  c
```

The hunk header `@@ -1,3 +1,3 @@` gives the **old** range (`-1,3`: start line
1, 3 lines) and the **new** range (`+1,3`: start line 1, 3 lines). Context
lines are prefixed with a space, deletions with `-`, additions with `+`.

## Edge cases that catch people

### Empty old file (pure insertion)

When the old buffer is empty, the hunk header uses `0,0` for the old range and
the start line is `0` (the conceptual line before the insertion point):

```csharp
Diff.UnifiedDiff("", "a\nb\nc\n");
// @@ -0,0 +1,3 @@
// +a
// +b
// +c
```

### Empty new file (pure deletion)

Symmetric: new range is `0,0`, start line is the line before the deletion:

```csharp
Diff.UnifiedDiff("a\nb\n", "");
// @@ -1,2 +0,0 @@
// -a
// -b
```

### No trailing newline

When an emitted input line lacks a trailing `\n`, the emitter adds a newline
and the `\ No newline at end of file` marker after that line. A missing newline
in an input does not itself force a hunk or marker to be emitted.

One side:

```csharp
Diff.UnifiedDiff("a\nb", "a\nb\nc\n");
// @@ -1,2 +1,3 @@
//  a
// -b
// \ No newline at end of file
// +b
// +c
```

Both sides:

```csharp
Diff.UnifiedDiff("a", "b");
// @@ -1 +1 @@
// -a
// \ No newline at end of file
// +b
// \ No newline at end of file
```

### Identical inputs

Returns `string.Empty` (string overload) or an empty `byte[]` (byte overload).
There is no hunk header, no `@@` line, no trailing newline — just emptiness.

```csharp
string patch = Diff.UnifiedDiff("a\nb\nc\n", "a\nb\nc\n");
// ""
```

## Controlling hunk shape

Two `DiffOptions` properties govern how the raw edit script is grouped into
hunks. See [diff-options.md](diff-options.md) for the full option reference.

### `ContextLines` — context window size

Number of unchanged lines to emit before and after each hunk's changes.
Default `3` (git's standard `--unified=3`). Set to `0` for no context
(`--unified=0`):

```csharp
Diff.UnifiedDiff(
    "a\nb\nc\nd\ne\n",
    "a\nb\nC\nd\ne\n",
    new DiffOptions { ContextLines = 1 });
// @@ -2,3 +2,3 @@
//  b
// -c
// +C
//  d
```

### `InterHunkLines` — split threshold

Maximum number of unchanged lines allowed between two adjacent changes within
the same hunk before they split. Default `0`. The effective split threshold is
`2 * ContextLines + InterHunkLines`.

With the default `ContextLines = 3` and `InterHunkLines = 0`, changes separated
by more than `2*3 + 0 = 6` unchanged lines split into two hunks:

```csharp
Diff.UnifiedDiff(
    "1\n2\n3\n4\n5\n6\n7\n8\n9\n",
    "A\n2\n3\n4\n5\n6\n7\n8\nB\n");
// @@ -1,4 +1,4 @@
// -1
// +A
//  2
//  3
//  4
// @@ -6,4 +6,4 @@
//  6
//  7
//  8
// -9
// +B
```

Changes within the threshold merge into one hunk:

```csharp
Diff.UnifiedDiff(
    "a\nb\nc\nd\ne\n",
    "X\nb\nc\nY\ne\n");
// @@ -1,5 +1,5 @@
// -a
// +X
//  b
//  c
// -d
// +Y
//  e
```

The two changes are only 2 lines apart (well inside `2*3 + 0 = 6`), so they
share a hunk.

## Bytes in, bytes out

The byte overload is the right choice when you want to avoid a UTF-8
encode/decode round-trip — for example, when you already have bytes from a file
read, or when the content might not be valid UTF-8:

```csharp
using System.IO;
using Xdiff;

byte[] oldBytes = await File.ReadAllBytesAsync("old.bin");
byte[] newBytes = await File.ReadAllBytesAsync("new.bin");

byte[] patchBytes = Diff.UnifiedDiff(oldBytes, newBytes);
await File.WriteAllBytesAsync("changes.patch", patchBytes);
```

The returned `byte[]` is freshly allocated; it does not alias the inputs.
Byte inputs are borrowed without copying during rendering. Keep their memory
valid and unmodified until the call returns. Inputs still undergo line-based
processing, without automatic binary detection.

## Writing to a buffer

```csharp
using System.Buffers;
using Xdiff;

var writer = new ArrayBufferWriter<byte>();
Diff.UnifiedDiff(writer, "a\n"u8.ToArray(), "b\n"u8.ToArray());
ReadOnlyMemory<byte> patch = writer.WrittenMemory;
```

Output is appended to any existing content. Xdiff does not clear or dispose the
writer; the caller owns its storage and lifetime. Writing completes before the
method returns. Do not write to the same writer concurrently or let output
writes modify borrowed input memory. If writing throws, the exception propagates
and bytes already appended remain; output is not rolled back.

## When to use this vs. `Diff.Compute`

| You want | Use |
|---|---|
| A patch string for display/storage | `Diff.UnifiedDiff` |
| Unified-diff bytes without UTF-8 conversion | `Diff.UnifiedDiff` (byte overload) |
| To count additions/deletions, classify lines, build a UI | `Diff.Compute` (see [diff-structured.md](diff-structured.md)) |
| A function-name annotation in the hunk header | Either — set `IncludeFunctionNames = true` (see [diff-options.md](diff-options.md)) |

`UnifiedDiff` is simpler and faster to consume when you just need the text.
`Compute` gives you structured `DiffHunk` / `DiffLine` objects you can walk in
code. It allocates result objects and collections while borrowing line and
function-name bytes. Its sink overload delivers callbacks without building
those result collections.