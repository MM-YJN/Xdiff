# Recipes

End-to-end recipes for common tasks. Each recipe is self-contained — copy the
snippet, fill in your inputs. All samples use top-level statements.

For the API reference behind any option, see
[diff-options.md](diff-options.md) and [merge-options.md](merge-options.md).

## Diff

### 1. Diff two files on disk and print the patch

```csharp
using System.IO;
using System.Text;
using Xdiff;

string oldText = await File.ReadAllTextAsync("old.txt");
string newText = await File.ReadAllTextAsync("new.txt");

string patch = Diff.UnifiedDiff(oldText, newText);
if (patch.Length > 0)
{
    Console.Write(patch);
}
else
{
    Console.WriteLine("(no changes)");
}
```

For a byte-exact pipeline that avoids the UTF-8 round-trip:

```csharp
using System.IO;
using Xdiff;

byte[] oldBytes = await File.ReadAllBytesAsync("old.bin");
byte[] newBytes = await File.ReadAllBytesAsync("new.bin");
byte[] patchBytes = Diff.UnifiedDiff(oldBytes, newBytes);
await File.WriteAllBytesAsync("changes.patch", patchBytes);
```

### 2. Show only the changed lines (no context)

Set `ContextLines = 0` (git's `--unified=0`):

```csharp
using Xdiff;

string patch = Diff.UnifiedDiff(
    "a\nb\nc\nd\ne\n",
    "a\nb\nC\nd\ne\n",
    new DiffOptions { ContextLines = 0 });
// @@ -3 +3 @@
// -c
// +C
```

### 3. Diff ignoring trailing-whitespace-only edits

`WhitespaceMode.IgnoreAtEol` treats lines that differ only in trailing
whitespace as equivalent. A diff that would have shown a one-line change
becomes empty:

```csharp
using Xdiff;

DiffResult result = Diff.Compute(
    "line one \nline two\n"u8.ToArray(),   // trailing space on line 1
    "line one\nline two\n"u8.ToArray(),
    new DiffOptions { Whitespace = WhitespaceMode.IgnoreAtEol });

Console.WriteLine(result.IsEmpty ? "no changes" : "changed");
// no changes
```

Other useful flags: `IgnoreChanges` (collapse internal whitespace runs),
`IgnoreAll` (skip all whitespace), `IgnoreCrAtEol` (treat `\r\n` and `\n` as
equivalent). See [diff-options.md](diff-options.md#whitespace) for the full
matrix.

### 4. Diff a reordered file with Patience

`Myers` (the default) can produce noisy output when lines are moved. `Patience`
anchors the diff on unique common lines, producing more readable hunks on
reorderings:

```csharp
using Xdiff;

string before = "a\nb\nc\nd\ne\nf\ng\n";
string after  = "g\nf\ne\nd\nc\nb\na\n";   // reverse order

string myers   = Diff.UnifiedDiff(before, after);
string patience = Diff.UnifiedDiff(before, after,
    new DiffOptions { Algorithm = DiffAlgorithm.Patience });

// `patience` typically anchors on the unique lines and produces
// fewer, more meaningful hunks than `myers` on this kind of input.
```

If your input has many duplicated lines that defeat Patience anchoring, use
`Histogram` — it picks the most-common rare line as the anchor and falls back
to Myers on degenerate regions.

### 5. Walk a `DiffResult` to count additions and deletions

```csharp
using Xdiff;

DiffResult result = Diff.Compute(oldData, newData);

int additions = 0, deletions = 0;
foreach (DiffHunk hunk in result.Hunks)
{
    foreach (DiffLine line in hunk.Lines)
    {
        switch (line.Kind)
        {
            case DiffLineKind.Addition: additions++; break;
            case DiffLineKind.Deletion: deletions++; break;
        }
    }
}

Console.WriteLine($"+{additions} -{deletions} in {result.Hunks.Count} hunk(s)");
```

### 6. Annotate hunks with function names

The default function-name matcher accepts any line whose first byte is an
ASCII letter, `_`, or `$`. For Python `def`, C# methods, JavaScript `function`,
etc., this is often enough:

```csharp
using Xdiff;

string before = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n";
string after  = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n";

string patch = Diff.UnifiedDiff(before, after,
    new DiffOptions { IncludeFunctionNames = true });
// @@ -2,4 +2,4 @@ def foo():
//      a = 1
//      b = 2
//      c = 3
//     -x = 1
//     +x = 2
```

### 7. Use a custom `FunctionMatcher` for non-default languages

For languages the default matcher doesn't recognize (e.g. markdown headings
starting with `#`), supply a `FunctionMatcher` predicate:

```csharp
using Xdiff;

string before = "# header\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n";
string after  = "# header\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n";

string patch = Diff.UnifiedDiff(before, after,
    new DiffOptions
    {
        IncludeFunctionNames = true,
        FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'#',
    });
// @@ -2,4 +2,4 @@ # header
//  ...
```

### 8. Add a per-language `FunctionNameExtractor` (capture-group extraction)

When the function name is a **substring** of the matching line (capture group
1), use `FunctionNameExtractor`. The reference implementation for
HTML/JavaScript/PHP lives at
`tests/Xdiff.UnitTests/UserDiffDrivers.cs`; here is a self-contained PHP
extractor:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Xdiff;

Regex[] phpPatterns =
[
    new(@"^[ \t]*(((public|private|protected|static|final)[ \t]+)*((class|function)[ \t].*))$",
        RegexOptions.None, TimeSpan.FromSeconds(5)),
];

(bool IsMatch, Range NameRange) PhpExtractor(ReadOnlySpan<byte> line)
{
    // 1. Right-trim the line before matching
    int end = line.Length;
    while (end > 0 && IsSpace(line[end - 1])) end--;
    ReadOnlySpan<byte> trimmed = line[..end];
    if (trimmed.IsEmpty) return (false, default);

    // 2. UTF-8 decode and try each pattern in order
    string text = Encoding.UTF8.GetString(trimmed);
    foreach (Regex re in phpPatterns)
    {
        Match m = re.Match(text);
        if (!m.Success) continue;

        // 3. group 1 if captured, else group 0 (full match)
        Group capture = m.Groups[1].Success ? m.Groups[1] : m.Groups[0];
        int start = capture.Index;
        int stop = start + capture.Length;

        // 4. trim whitespace from the capture
        while (start < stop && char.IsWhiteSpace(text[start])) start++;
        while (stop > start && char.IsWhiteSpace(text[stop - 1])) stop--;
        if (start >= stop) return (false, default);

        // 5. map char indices -> byte offsets into `trimmed`
        int byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
        int byteEnd   = byteStart + Encoding.UTF8.GetByteCount(text.AsSpan(start, stop - start));
        return (true, new Range(byteStart, byteEnd));
    }
    return (false, default);

    static bool IsSpace(byte c) => c is (byte)' ' or (byte)'\t' or (byte)'\n'
        or (byte)'\r' or (byte)'\f' or (byte)'\v';
}

string phpBefore = "    public static function foo($x) {\n        $value = $x;\n        return $value;\n    }\n";
string phpAfter  = "    public static function foo($x) {\n        $value = $x;\n        return $value + 1;\n    }\n";

string patch = Diff.UnifiedDiff(phpBefore, phpAfter,
    new DiffOptions
    {
        ContextLines = 1,
        InterHunkLines = 1,
        IncludeFunctionNames = true,
        FunctionNameExtractor = PhpExtractor,
    });

Console.Write(patch);
```

The unchanged declaration precedes the hunk's context. Capture group 1 includes
the modifiers, and the extractor removes leading indentation, producing:

```diff
@@ -2,3 +2,3 @@ public static function foo($x) {
         $value = $x;
-        return $value;
+        return $value + 1;
     }
```

## Merge

### 9. Merge a feature branch with diff3 + commit labels

`MergeStyle.Diff3` includes the ancestor block so reviewers can see the
original text. Commit SHAs are common labels:

```csharp
using System.Text;
using Xdiff;

byte[] ancestor = await File.ReadAllBytesAsync("ancestor.txt");
byte[] ours     = await File.ReadAllBytesAsync("ours.txt");
byte[] theirs   = await File.ReadAllBytesAsync("theirs.txt");

var opts = new MergeOptions
{
    Style         = MergeStyle.Diff3,
    OurLabel      = "HEAD",
    TheirLabel    = "7cb63eed597130ba4abb87b3e544b85021905520",
    AncestorLabel = "initial",
};

MergeResult r = Merger.Merge(ancestor, ours, theirs, opts);
Console.WriteLine(r.HasConflicts
    ? $"merge produced {r.ConflictCount} conflict(s)"
    : "clean merge");
Console.Write(Encoding.UTF8.GetString(r.Content));
```

Output for a conflict:

```
<<<<<<< HEAD
this file is changed in master and branch
||||||| initial
this file is a conflict
=======
this file is changed in branch and master
>>>>>>> 7cb63eed597130ba4abb87b3e544b85021905520
```

### 10. Resolve conflicts by favoring ours

`MergeFavor.Ours` resolves conflicting regions by taking our side while
preserving non-conflicting changes from both sides. Here both sides change the
title, and theirs also makes an independent footer edit:

```csharp
using System.Text;
using Xdiff;

byte[] ancestor = "title\nbody one\nbody two\nbody three\nfooter\n"u8.ToArray();
byte[] ours = "our title\nbody one\nbody two\nbody three\nfooter\n"u8.ToArray();
byte[] theirs = "their title\nbody one\nbody two\nbody three\nnew footer\n"u8.ToArray();

MergeResult r = Merger.Merge(
    ancestor, ours, theirs,
    new MergeOptions { Favor = MergeFavor.Ours });

Console.WriteLine($"Unresolved conflicts: {r.ConflictCount}");
Console.Write(Encoding.UTF8.GetString(r.Content));
```

```text
Unresolved conflicts: 0
our title
body one
body two
body three
new footer
```

The result contains our title and their footer edit. Symmetrically,
`MergeFavor.Theirs` takes their side for each conflict while preserving
non-conflicting changes from both sides.

### 11. Union-merge divergent additions

`MergeFavor.Union` concatenates both sides (ours then theirs) for every
conflict. Useful when both branches add distinct content that should both be
kept (e.g. different import statements):

```csharp
using Xdiff;

MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),   // ours adds X
    "a\nY\nc\n"u8.ToArray(),   // theirs adds Y
    new MergeOptions { Favor = MergeFavor.Union });
// r.ConflictCount == 0
// r.Content as text == "a\nX\nY\nc\n"   (both X and Y appear)
```

`Union` is **not** useful for contradictory edits to the same line — it will
produce both versions concatenated, which is rarely what you want. For
"keep both sides' distinct additions," it is exactly right.

### 12. Produce zdiff3 conflicts for cleaner review

`MergeStyle.ZealousDiff3` pulls common leading and trailing lines out of the
conflict markers, so the markers contain only the genuinely disagreeing lines:

```csharp
using Xdiff;

var opts = new MergeOptions
{
    Style      = MergeStyle.ZealousDiff3,
    OurLabel   = "ours",
    TheirLabel = "theirs",
};

MergeResult r = Merger.Merge(ancestor, ours, theirs, opts);
// Where a normal diff3 conflict would wrap an entire function in markers,
// ZealousDiff3 emits only the differing lines inside the markers and
// leaves the shared surrounding code outside.
```

### 13. Tighten conflicts with `ZealousAlnum`

The default `Level = Zealous` merges adjacent conflicts separated by ≤ 3
unchanged lines regardless of the gap content. `ZealousAlnum` only merges
when the gap has no alphanumeric (C-locale ASCII) bytes, keeping conflicts
separate across meaningful code:

```csharp
using Xdiff;

// Gap is punctuation/whitespace only -> merged
MergeResult merged = Merger.Merge(ancestor, ours, theirs,
    new MergeOptions { Level = MergeLevel.ZealousAlnum });
// merged.ConflictCount == 1

// Gap contains an identifier -> kept separate
MergeResult separate = Merger.Merge(ancestor2, ours2, theirs2,
    new MergeOptions { Level = MergeLevel.ZealousAlnum });
// separate.ConflictCount == 2
```

### 14. Merge with wider conflict markers

If your consumer's tooling collides on the default 7-character markers, set
`MarkerSize`:

```csharp
using Xdiff;

MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),
    "a\nY\nc\n"u8.ToArray(),
    new MergeOptions { MarkerSize = 9 });
// Content contains <<<<<<<<< (9 chars), =========, >>>>>>>>>
```

Values ≤ 0 fall back to 7.

### 15. Binary-safe merge guard

Xdiff treats all input as text. Before merging possibly-binary content, check
for a NUL byte in the first 8000 bytes:

```csharp
using System.Text;
using Xdiff;

static bool IsBinary(ReadOnlySpan<byte> data)
{
    int scan = Math.Min(data.Length, 8000);
    for (int i = 0; i < scan; i++)
    {
        if (data[i] == 0) return true;
    }
    return false;
}

byte[] ancestor = File.ReadAllBytes("ancestor.bin");
byte[] ours     = File.ReadAllBytes("ours.bin");
byte[] theirs   = File.ReadAllBytes("theirs.bin");

if (IsBinary(ours) || IsBinary(theirs) || IsBinary(ancestor))
{
    Console.Error.WriteLine("binary conflict: refusing to text-merge");
    Environment.Exit(1);
}

MergeResult r = Merger.Merge(ancestor, ours, theirs);
Console.Write(Encoding.UTF8.GetString(r.Content));
```

### 16. Bytes-in/bytes-out pipeline (no UTF-8 round-trip)

For non-text content or byte-exact workflows, use the `ReadOnlyMemory<byte>`
overload and stay in bytes the whole way through:

```csharp
using Xdiff;

ReadOnlyMemory<byte> ancestor = File.ReadAllBytes("ancestor.dat");
ReadOnlyMemory<byte> ours     = File.ReadAllBytes("ours.dat");
ReadOnlyMemory<byte> theirs   = File.ReadAllBytes("theirs.dat");

MergeResult r = Merger.Merge(ancestor, ours, theirs);
File.WriteAllBytes("merged.dat", r.Content);
// r.Content is a fresh byte[]; it does not alias the input buffers.
```

This avoids any UTF-8 encode/decode overhead and preserves byte-exact output
for content that isn't valid UTF-8.
