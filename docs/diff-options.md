# `DiffOptions` reference

`DiffOptions` is a `sealed record` with `init` properties. Construct it with an
object initializer and pass it (or `null` for defaults) to `Diff.Compute` or
`Diff.UnifiedDiff`.

```csharp
public sealed record DiffOptions
{
    public DiffAlgorithm    Algorithm              { get; init; } = DiffAlgorithm.Myers;
    public WhitespaceMode   Whitespace             { get; init; }
    public bool             IgnoreBlankLines       { get; init; }
    public bool             IndentHeuristic        { get; init; }
    public int              ContextLines           { get; init; } = 3;
    public int              InterHunkLines         { get; init; }
    public bool             IncludeFunctionNames   { get; init; }
    public bool             FunctionContext        { get; init; }
    public Func<ReadOnlySpan<byte>, bool>?                            FunctionMatcher     { get; init; }
    public Func<ReadOnlySpan<byte>, (bool IsMatch, Range NameRange)>? FunctionNameExtractor { get; init; }
}
```

Use the `with` expression to vary one option from a base:

```csharp
var baseOpts = new DiffOptions { ContextLines = 1, InterHunkLines = 1, IncludeFunctionNames = true };
var hist = baseOpts with { Algorithm = DiffAlgorithm.Histogram };
var pat = baseOpts with { Algorithm = DiffAlgorithm.Patience };
```

---

## `Algorithm`

```csharp
public enum DiffAlgorithm { Myers, Minimal, Patience, Histogram }
```

Default: `Myers`.

All four produce a **minimal** edit script by definition; they differ in how
they pick among equally-short scripts and how they handle pathological inputs.

| Value | Behaviour | Use when |
|---|---|---|
| `Myers` | Default. O(ND) with forward/backward snake heuristics (`XDL_SNAKE_CNT=20`, `XDL_K_HEUR=4`). Fast on typical inputs; may produce slightly non-minimal output on adversarial cases. | General use. The default is right for almost everything. |
| `Minimal` | Myers O(ND) with the heuristic short-circuits disabled (`need_min=true`). Slower because it always walks the full diagonal frontier, but guaranteed minimum-length. | You need a guaranteed-minimal script and can pay the cost. |
| `Patience` | Anchors the script on unique common lines (longest increasing subsequence) and recurses into the gaps. More human-readable on reorderings. Skips the trim/cleanup pre-pass used by Myers. | Reorderings or moves produce noisy Myers output; you want hunks anchored on stable unique lines. |
| `Histogram` | Like Patience but uses the most-common rare line as the anchor and falls back to Myers on degenerate regions. Robust against duplicated lines that confuse Patience. Skips the trim/cleanup pre-pass. | Inputs have many duplicated lines that defeat Patience anchoring. |

### When do they actually differ?

On simple inputs, all four produce byte-identical output:

```csharp
var opts = new DiffOptions { ContextLines = 1, InterHunkLines = 1, IncludeFunctionNames = true };
byte[] myers  = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Myers });
byte[] min    = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Minimal });
byte[] pat    = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Patience });
byte[] hist   = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Histogram });
// myers == min == pat == hist  on benign inputs
```

They diverge on:

- **Shuffled/duplicated content** — Patience and Histogram produce more
  readable hunks anchored on meaningful lines.
- **Adversarial inputs** — Minimal guarantees the shortest script; Myers's
  heuristic may emit a slightly longer one.

If you don't know which to use, leave the default `Myers`.

---

## `Whitespace`

```csharp
[Flags]
public enum WhitespaceMode
{
    None          = 0,
    IgnoreAll     = 1,
    IgnoreChanges = 2,
    IgnoreAtEol   = 4,
    IgnoreCrAtEol = 8,
}
```

Default: `None` (every byte, including whitespace, participates).

The flags control how lines are hashed and compared. Two lines match when
their non-ignored bytes are identical in order. Flags can be combined where it
makes sense, e.g. `IgnoreChanges | IgnoreCrAtEol`.

The classifier checks flags in this order and stops at the first that matches:
`IgnoreAll` → `IgnoreChanges` → `IgnoreAtEol` → `IgnoreCrAtEol`.

### What each flag does

| Flag | Matches | Does not match |
|---|---|---|
| `None` | exact bytes only | `a b` ≠ `ab`; `ab ` ≠ `ab`; `ab\n` ≠ `ab\r\n` |
| `IgnoreAll` | skip all whitespace runs entirely | `a b` == `ab`; `a  b` == `a b` |
| `IgnoreChanges` | collapse consecutive whitespace to a single space; **both sides must have whitespace at the same logical position**; trailing whitespace always ignored | `a b` == `a  b`; `a\tb` == `a b` — but `a b` ≠ `ab` (missing whitespace is still significant) |
| `IgnoreAtEol` | ignore only trailing whitespace before the newline | `ab ` == `ab` — but `a b` ≠ `ab` (leading/inter-token whitespace still significant) |
| `IgnoreCrAtEol` | ignore a single `\r` immediately preceding the terminating `\n`; treats `\r\n` and `\n` as equivalent | `ab\n` == `ab\r\n` — but a lone trailing `\r` on an incomplete line (no `\n`) is still significant |

### Example

```csharp
using Xdiff;

var opts = new DiffOptions { Whitespace = WhitespaceMode.IgnoreAtEol };
DiffResult result = Diff.Compute(
    "line one \nline two\n"u8.ToArray(),   // trailing space on line 1
    "line one\nline two\n"u8.ToArray(),    // no trailing space
    opts);
// result.IsEmpty == true — the only difference is trailing whitespace,
// which IgnoreAtEol treats as equivalent.
```

---

## `IgnoreBlankLines`

Default: `false`.

When `true`, hunks whose changed lines are **all** blank (per `Whitespace`)
are marked ignorable and dropped from the emitted output, unless they merge
with non-ignorable hunks. Equivalent to git's `--ignore-blank-lines`.

This is **distinct** from `WhitespaceMode.IgnoreAll`: `IgnoreAll` makes
`"a b"` and `"ab"` equivalent during comparison; `IgnoreBlankLines` drops
hunks that only add or remove empty lines.

```csharp
using Xdiff;

var opts = new DiffOptions { IgnoreBlankLines = true };
DiffResult result = Diff.Compute("a\n\nb\n"u8.ToArray(), "a\nb\n"u8.ToArray(), opts);
// result.IsEmpty == true — the only change is a removed blank line, which
// IgnoreBlankLines suppresses.

DiffResult result2 = Diff.Compute("a\n\nb\n"u8.ToArray(), "a\nb\n"u8.ToArray());
// result2.Hunks.Count == 1 — without the flag, the blank-line removal shows.
```

---

## `IndentHeuristic`

Default: `false`.

When `true`, runs the indent heuristic on the raw edit script to shift hunks
toward nicer indentation boundaries. Equivalent to git's
`--indent-heuristic`. Like the reference implementation (which passes the flag
to `xdl_change_compact()` regardless of the chosen algorithm), it is applied
after every algorithm — Myers, Minimal, Patience, and Histogram alike.

The heuristic looks at indentation levels around a change group and shifts the
hunk boundary so the surrounding context lines have consistent indent. It
changes **where** hunks start and end, not **whether** changes are detected.

```csharp
using Xdiff;

var with    = new DiffOptions { IndentHeuristic = true,  ContextLines = 3 };
var without = new DiffOptions { IndentHeuristic = false, ContextLines = 3 };

string a = Diff.UnifiedDiff(before, after, with);
string b = Diff.UnifiedDiff(before, after, without);
// On inputs with inconsistent indentation around changes, a != b
// and `a` has more visually coherent hunk boundaries.
```

The xdiff golden suite covers six scenarios: blank-line shift, function-block
insertion, tab-indented sliding, max-indent clamping, many-blanks handling,
and change-at-start-of-file.

---

## `ContextLines` and `InterHunkLines`

See [diff-text.md](diff-text.md#controlling-hunk-shape) for the worked examples.

- `ContextLines` (default `3`): unchanged context lines to emit before and
  after each hunk. `0` = no context.
- `InterHunkLines` (default `0`): max unchanged lines between two adjacent
  changes within a hunk before they split. Effective split threshold is
  `2 * ContextLines + InterHunkLines`.

Both properties accept `0` through `int.MaxValue`, inclusive. Options can be
constructed with any value, but all `Diff.Compute` and `Diff.UnifiedDiff`
overloads reject negative values with `ArgumentOutOfRangeException` before
processing inputs, even when inputs are empty or identical. `ParamName` names
the invalid property and `ActualValue` contains its supplied value.
`ContextLines` is validated before `InterHunkLines`.

---

## `IncludeFunctionNames`

Default: `false`.

When `true`, appends the function-name annotation of the nearest preceding
matching line to each hunk header — the text after `@@ -a,b +c,d @@` in
unified-diff text.

The "function line" is found by walking backward from the hunk start to the
first line that satisfies the configured function matcher (see
`FunctionMatcher` / `FunctionNameExtractor` below).

```csharp
using Xdiff;

byte[] oldData = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n"u8.ToArray();
byte[] newData = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n"u8.ToArray();
var opts = new DiffOptions { IncludeFunctionNames = true };

DiffResult result = Diff.Compute(oldData, newData, opts);
DiffHunk hunk = result.Hunks[0];
// hunk.FunctionName is the raw bytes of "def foo():" (undecoded)
Assert.Equal("def foo():"u8.ToArray(), hunk.FunctionName.ToArray());

string text = Diff.UnifiedDiff(
    "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n",
    "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n",
    opts);
// @@ -2,4 +2,4 @@ def foo():
//  ...
```

The default matcher accepts any line whose first byte is an ASCII letter,
`_`, or `$` (xdiff's `def_ff`). For language-specific matching, supply
`FunctionMatcher` or `FunctionNameExtractor`.

### The 80-byte cap

When using the default matcher (or a `FunctionMatcher` predicate without an
extractor), the emitted function name is the whole matched line, trimmed of
trailing whitespace and **capped at 80 bytes**. This mirrors xdiff's
`struct func_line { char buf[80]; }`. If your function line is longer than 80
bytes, only the first 80 bytes appear in the hunk header. The
`FunctionNameExtractor` path is not subject to this cap — it returns exactly
the byte range you specify.

---

## `FunctionMatcher`

```csharp
public Func<ReadOnlySpan<byte>, bool>? FunctionMatcher { get; init; }
```

Default: `null` (use the built-in `def_ff` first-byte matcher).

A predicate that decides whether a given line is a "function line." When
non-null, it replaces the default matcher. Used by both `IncludeFunctionNames`
and `FunctionContext`.

The span is the raw line bytes (no leading sigil, includes the line
terminator). Return `true` if the line is a function header.

```csharp
using Xdiff;

// Match lines starting with '#' (markdown headings, shell comments, etc.)
var opts = new DiffOptions
{
    IncludeFunctionNames = true,
    FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'#',
};

string output = Diff.UnifiedDiff(
    "# header\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n",
    "# header\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n",
    opts);
// @@ -2,4 +2,4 @@ # header
//  ...
```

---

## `FunctionNameExtractor`

```csharp
public Func<ReadOnlySpan<byte>, (bool IsMatch, Range NameRange)>? FunctionNameExtractor { get; init; }
```

Default: `null`.

The most powerful function-name hook. Given a candidate line, returns whether
it is a function line and, if so, the **byte range** of the name text to emit
after `@@ ... @@`. When non-null, this takes precedence over `FunctionMatcher`
for both the match decision and the name text.

### Why it exists

Per-language function-name regexes can extract
**capture group 1** (not the whole line) as the function name. A plain
`FunctionMatcher` predicate cannot return the captured substring, so the
extractor delegate returns a `Range` into the input span instead.

The returned `Range` may cover any slice of the input — a capture group, the
whole line, or a substring. The hunk's `FunctionName` borrows that slice of the
old input buffer without copying. Keep its memory valid and unmodified while
using the result, or call `FunctionName.ToArray()` for an independent copy.

### A complete extractor example

This is a trimmed version of the reference implementation in
`tests/Xdiff.UnitTests/UserDiffDrivers.cs`, which contains
built-in HTML/JavaScript/PHP userdiff driver patterns verbatim. The algorithm
works as follows:

1. Right-trim the line of trailing whitespace.
2. Try each regex pattern in order; use the first that matches.
3. Use capture group 1 if it participated, else group 0 (the full match).
4. Trim leading/trailing whitespace from the capture.
5. Map character indices back to byte offsets into the (rtrimmed) line.

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Xdiff;

// PHP: "public static function foo(...)" -> name is "function foo(...)"
private static readonly Regex[] PhpPatterns =
[
    new(@"^[ \t]*(((public|private|protected|static|final)[ \t]+)*((class|function)[ \t].*))$",
        RegexOptions.None, TimeSpan.FromSeconds(5)),
];

public static (bool IsMatch, Range NameRange) PhpExtractor(ReadOnlySpan<byte> line)
{
    // 1. rtrim
    int end = line.Length;
    while (end > 0 && IsSpace(line[end - 1])) end--;
    ReadOnlySpan<byte> trimmed = line[..end];
    if (trimmed.IsEmpty) return (false, default);

    // 2. match
    string text = Encoding.UTF8.GetString(trimmed);
    foreach (Regex re in PhpPatterns)
    {
        Match m = re.Match(text);
        if (!m.Success) continue;

        // 3. group 1 if captured, else group 0
        Group capture = m.Groups[1].Success ? m.Groups[1] : m.Groups[0];
        int start = capture.Index;
        int stop = start + capture.Length;

        // 4. trim whitespace from the capture
        while (start < stop && char.IsWhiteSpace(text[start])) start++;
        while (stop > start && char.IsWhiteSpace(text[stop - 1])) stop--;
        if (start >= stop) return (false, default);

        // 5. char index -> byte offset (matters when UTF-8 bytes precede the match)
        int byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
        int byteEnd   = byteStart + Encoding.UTF8.GetByteCount(text.AsSpan(start, stop - start));
        return (true, new Range(byteStart, byteEnd));
    }

    return (false, default);

    static bool IsSpace(byte c) => c is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r'
        or (byte)'\f' or (byte)'\v';
}

// Usage
var opts = new DiffOptions
{
    ContextLines = 1,
    InterHunkLines = 1,
    IncludeFunctionNames = true,
    FunctionNameExtractor = PhpExtractor,
};
string patch = Diff.UnifiedDiff(phpBefore, phpAfter, opts);
```

See `UserDiffDrivers.cs` for the full HTML/JavaScript/PHP patterns and the
generic `BuildExtractor(string driver)` helper.

### Precedence

When more than one function hook is configured, the emitter uses exactly one,
in this order:

1. `FunctionNameExtractor` (if non-null) — decides match and provides name text.
2. `FunctionMatcher` (if non-null) — decides match; name text is the whole
   line trimmed, capped at 80 bytes.
3. Default `def_ff` — first byte is ASCII letter / `_` / `$`.

Set at most one of `FunctionNameExtractor` / `FunctionMatcher` to avoid
ambiguity.

---

## `FunctionContext`

Default: `false`.

When `true`, expands each hunk to cover the entire enclosing function block
(`XDL_EMIT_FUNCCONTEXT`, git's `-W` / `--function-context`). The hunk grows
backward to the nearest preceding function line (skipping blank lines) and
forward to the end of the current function.

Ported for fidelity with xdiff's emitter; useful for "show me the whole
function containing this change" views.

```csharp
using Xdiff;

var opts = new DiffOptions
{
    FunctionContext = true,
    IncludeFunctionNames = true,
    FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'h',
};

string output = Diff.UnifiedDiff(before, after, opts);
// Each hunk now spans from the nearest preceding 'h'-prefixed line through
// the end of the function block containing the change.
```

### Backward walk skips blank lines

When growing the hunk start backward, the emitter skips blank records that
immediately precede a function line. So a change inside a function body finds
the function header even if a blank line separates them:

```csharp
string before =
    "preamble\n" +
    "header alpha\n" +   // function line (starts with 'h')
    "\n",                // blank line — skipped by the backward walk
    "    body1 = 1\n" +
    "    body2 = 2\n";
string after =
    "preamble\n" +
    "header alpha\n" +
    "\n" +
    "    body1 = 1\n" +
    "    body2 = 9\n";    // changed line
// The hunk for the body2 change extends back to "header alpha".
```

### Forward scan at end of file

When a change is at the end of the old file (an insertion-only region), the
emitter walks forward into the new file looking for a function record and
appends the whole trailing function to the hunk end.

Use `FunctionContext` together with `IncludeFunctionNames` to get both the
expanded range and the function-name annotation in the hunk header.