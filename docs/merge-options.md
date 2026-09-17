# `MergeOptions` reference

`MergeOptions` is a `sealed record` with `init` properties. Construct it with
an object initializer and pass it (or `null` for defaults) to `Merger.Merge`.

```csharp
public sealed record MergeOptions
{
    public DiffAlgorithm  Algorithm      { get; init; } = DiffAlgorithm.Myers;
    public WhitespaceMode Whitespace     { get; init; }
    public MergeLevel     Level          { get; init; } = MergeLevel.Zealous;
    public MergeFavor     Favor          { get; init; }
    public MergeStyle     Style          { get; init; }
    public int            MarkerSize     { get; init; } = 7;
    public string?        AncestorLabel  { get; init; }
    public string?        OurLabel       { get; init; }
    public string?        TheirLabel     { get; init; }
}
```

> The indent heuristic is intentionally **not** exposed here. This API does not
> enable it on the merge path, so the internal ancestor↔side diffs always run
> with the indent heuristic disabled. See `MergeOptions.cs:8`.

Use the `with` expression to vary one option:

```csharp
var baseOpts = new MergeOptions { Style = MergeStyle.Diff3, OurLabel = "HEAD", TheirLabel = "topic" };
var zealous  = baseOpts with { Style = MergeStyle.ZealousDiff3 };
```

---

## `Algorithm`

Default: `Myers`.

The `DiffAlgorithm` used for the two underlying ancestor↔ours and
ancestor↔theirs diffs. See [diff-options.md](diff-options.md#algorithm) for the
full algorithm guide. The same four values are available (`Myers`, `Minimal`,
`Patience`, `Histogram`).

For most merge workloads the default `Myers` is correct — the merge algorithm
is dominated by region classification, not script minimality.

---

## `Whitespace`

Default: `None`.

The `WhitespaceMode` flags applied during the merge's line comparison. See
[diff-options.md](diff-options.md#whitespace) for the flag semantics.

Setting a whitespace flag can turn a would-be conflict into a clean merge when
the only difference between sides is whitespace:

```csharp
using Xdiff;

// Without whitespace flags: both sides changed the same line differently -> conflict
MergeResult conflict = Merger.Merge(
    ancestor, ours, theirs);
// conflict.ConflictCount >= 1

// With IgnoreAtEol: trailing-whitespace-only differences are equivalent -> clean
MergeResult clean = Merger.Merge(
    ancestor, ours, theirs,
    new MergeOptions { Whitespace = WhitespaceMode.IgnoreAtEol });
// clean.ConflictCount == 0
```

The same effect holds for `IgnoreChanges` (internal whitespace run differences)
and `IgnoreAll` (all whitespace).

---

## `Style`

```csharp
public enum MergeStyle { Merge = 0, Diff3 = 1, ZealousDiff3 = 2 }
```

Default: `Merge`.

Format of conflict markers emitted for unresolved conflicts.

### `Merge` (default)

Standard two-way merge markers:

```
<<<<<<<
ours
=======
theirs
>>>>>>>
```

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),
    "a\nY\nc\n"u8.ToArray());
// a
// <<<<<<<
// X
// =======
// Y
// >>>>>>>
// c
```

### `Diff3`

Includes the ancestor block between `|||||||` and `=======` so the reader can
see the original text:

```
<<<<<<<
ours
|||||||
ancestor
=======
theirs
>>>>>>>
```

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),
    "a\nY\nc\n"u8.ToArray(),
    new MergeOptions { Style = MergeStyle.Diff3 });
// a
// <<<<<<<
// X
// |||||||
// b
// =======
// Y
// >>>>>>>
// c
```

> **Level clamp.** `Diff3` forces `MergeLevel` to at most `Eager` because its
> conflict refinement takes a different path. See `Level` below.

### `ZealousDiff3`

Diff3 plus zdiff3-style conflict refinement: common leading and trailing
lines **inside** each conflict are pulled out of the conflict markers. This
produces smaller, more readable conflicts that show only the genuinely
disagreeing lines:

```csharp
using Xdiff;

var opts = new MergeOptions
{
    Style     = MergeStyle.ZealousDiff3,
    OurLabel  = "ours",
    TheirLabel = "theirs",
};

MergeResult r = Merger.Merge(ancestor, ours, theirs, opts);
// Where diff3 would emit:
//   <<<<<<<
//   1,
//   2,
//   X          <- only this line actually conflicts
//   3,
//   =======
//   1,
//   2,
//   Y
//   3,
//   >>>>>>>
//
// ZealousDiff3 emits:
//   1,
//   2,
//   <<<<<<< ours
//   X
//   =======
//   Y
//   >>>>>>> theirs
//   3,
//
// The common "1," and "3," lines are outside the markers.
```

---

## `Favor`

```csharp
public enum MergeFavor { Default = 0, Ours = 1, Theirs = 2, Union = 3 }
```

Default: `Default`.

Resolution policy applied to each conflict region after the three-way merge
has classified regions. When set to anything other than `Default`, every
conflict is resolved in favour of the chosen side and
`MergeResult.ConflictCount` is reported as `0`.

| Value | Behaviour |
|---|---|
| `Default` | Leave conflicts in place with conflict markers. `ConflictCount` may be > 0. |
| `Ours` | Take our side for every conflict. |
| `Theirs` | Take their side for every conflict. |
| `Union` | Concatenate both sides (ours followed by theirs) for every conflict. |

### Examples

```csharp
using Xdiff;

var ancestor = "a\nb\nc\n"u8.ToArray();
var ours     = "a\nX\nc\n"u8.ToArray();
var theirs   = "a\nY\nc\n"u8.ToArray();

MergeResult favorOurs = Merger.Merge(ancestor, ours, theirs,
    new MergeOptions { Favor = MergeFavor.Ours });
// favorOurs.ConflictCount == 0
// favorOurs.Content == "a\nX\nc\n"  (ours wins)

MergeResult favorTheirs = Merger.Merge(ancestor, ours, theirs,
    new MergeOptions { Favor = MergeFavor.Theirs });
// favorTheirs.ConflictCount == 0
// favorTheirs.Content == "a\nY\nc\n"  (theirs wins)

MergeResult favorUnion = Merger.Merge(ancestor, ours, theirs,
    new MergeOptions { Favor = MergeFavor.Union });
// favorUnion.ConflictCount == 0
// favorUnion.Content == "a\nX\nY\nc\n"  (both sides concatenated)
```

`Union` is useful when both sides add distinct content that should both be
kept (e.g. two branches adding different import statements). It is not useful
when the sides make contradictory edits to the same line.

---

## `Level`

```csharp
public enum MergeLevel { Minimal = 0, Eager = 1, Zealous = 2, ZealousAlnum = 3 }
```

Default: `Zealous`.

How aggressively to refine raw conflict regions into smaller, more accurate
conflicts. Levels are cumulative: `Eager` adds conflict refinement on top of
`Minimal`; `Zealous` adds non-conflict simplification on top of `Eager`;
`ZealousAlnum` tightens the simplification rule.

> **Diff3 clamp.** `MergeStyle.Diff3` and `MergeStyle.ZealousDiff3` clamp
> `Level` to at most `Eager` because their conflict refinement takes a
> different path. Setting `Level = Zealous` with a diff3 style behaves as
> `Eager`. See `MergeOptions.cs:28`.

### `Minimal`

No refinement. Every overlapping change becomes one big conflict region. The
fastest level and the coarsest. Notably, when both sides make the **same**
change, `Minimal` still reports it as a conflict:

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),   // both sides
    "a\nX\nc\n"u8.ToArray(),   //  b -> X
    new MergeOptions { Level = MergeLevel.Minimal });
// r.ConflictCount == 1   (identical changes still conflict at Minimal)
```

Use `Minimal` only when you need raw, unrefined conflicts or maximum speed.

### `Eager`

Recursively re-diffs each conflict region (ours vs theirs) and splits it into
independent conflicts and agreed-upon regions. Identical changes no longer
conflict; multi-line conflicts get split into per-line conflicts where the
sides actually disagree.

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\nd\ne\n"u8.ToArray(),
    "a\nX\nc\nY\ne\n"u8.ToArray(),
    "a\nZ\nc\nW\ne\n"u8.ToArray(),
    new MergeOptions { Level = MergeLevel.Eager });
// r.ConflictCount == 2   (two distinct conflicts, reported separately)
```

### `Zealous` (default)

`Eager` plus simplification: adjacent conflicts separated by a small gap (≤ 3
unchanged lines) are merged back into a single conflict. This produces fewer,
larger conflicts which are usually easier to review than many tiny ones.

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\nd\ne\n"u8.ToArray(),
    "a\nX\nc\nY\ne\n"u8.ToArray(),
    "a\nZ\nc\nW\ne\n"u8.ToArray());
// r.ConflictCount == 1   (the two conflicts 1 line apart merge into one)
```

If the gap is more than 3 unchanged lines, the conflicts stay separate:

```csharp
// (4 unchanged lines between the two changes)
// r.ConflictCount == 2   (gap > 3 prevents merging)
```

### `ZealousAlnum`

Like `Zealous` but the simplification step only fires when the gap between
adjacent conflicts contains no alphanumeric (C-locale ASCII) bytes. This
prevents merging conflicts across meaningful code (where the gap has
identifiers) while still merging across pure punctuation/whitespace.

```csharp
// Gap is punctuation-only -> merged (same as Zealous)
MergeResult r1 = Merger.Merge(ancestor, ours, theirs,
    new MergeOptions { Level = MergeLevel.ZealousAlnum });
// r1.ConflictCount == 1   (no alnum in the gap, so merge)

// Gap contains letters/digits -> kept separate (Zealous would have merged)
MergeResult r2 = Merger.Merge(ancestor2, ours2, theirs2,
    new MergeOptions { Level = MergeLevel.ZealousAlnum });
// r2.ConflictCount == 2   (alnum in the gap, so do not merge)
```

The alnum check uses C-locale ASCII ranges (`A`-`Z`, `a`-`z`, `0`-`9`) — not
`char.IsLetterOrDigit`, which would be Unicode-aware. This matches git's
`--zealous-alnum` semantics exactly.

### Choosing a level

| Level | Conflicts reported | Speed | Use when |
|---|---|---|---|
| `Minimal` | coarsest (one big conflict per overlap) | fastest | raw/unrefined output; maximum speed |
| `Eager` | per-disagreement | medium | you want precise conflicts but never want adjacent ones merged |
| `Zealous` | balanced (default) | medium | general use; reviewable conflicts |
| `ZealousAlnum` | precise across code | medium | you want merging across punctuation/whitespace gaps but not across identifiers |

---

## `MarkerSize`

Default: `7`.

Number of characters used to build each conflict marker
(`<<<<<<<`, `=======`, `>>>>>>>`, and diff3's `|||||||`). Values ≤ 0 fall back
to `7`.

```csharp
MergeResult r = Merger.Merge(
    "a\nb\nc\n"u8.ToArray(),
    "a\nX\nc\n"u8.ToArray(),
    "a\nY\nc\n"u8.ToArray(),
    new MergeOptions { MarkerSize = 9 });
// Content contains:
// <<<<<<<<<   (9 chars)
// X
// =========
// Y
// >>>>>>>>>>
```

Use a non-default size when your consumer's tooling collides on the standard
7-character markers, or to match a specific format requirement.

---

## `OurLabel`, `TheirLabel`, `AncestorLabel`

Default: all `null`.

Optional strings appended to the conflict markers. When non-null, the label
follows the marker rune separated by a space:

- `OurLabel` → appended to `<<<<<<<`
- `TheirLabel` → appended to `>>>>>>>`
- `AncestorLabel` → appended to `|||||||` (only emitted by `Diff3` /
  `ZealousDiff3` styles; ignored for `Merge` style)

`null` omits the label — the marker is emitted bare.

```csharp
using Xdiff;

var opts = new MergeOptions
{
    Style         = MergeStyle.Diff3,
    OurLabel      = "HEAD",
    TheirLabel    = "topic",
    AncestorLabel = "initial",
};

MergeResult r = Merger.Merge(ancestor, ours, theirs, opts);
// Content contains:
// <<<<<<< HEAD
// ours content
// ||||||| initial
// ancestor content
// =======
// theirs content
// >>>>>>> topic
```

Real-world labels are often commit SHAs, branch names, or file paths. The
labels are UTF-8 encoded when emitted; any string is acceptable:

```csharp
var opts = new MergeOptions
{
    OurLabel   = "HEAD",
    TheirLabel = "7cb63eed597130ba4abb87b3e544b85021905520",  // 40-char SHA works
};
```

The label text appears verbatim in the output. Make sure it does not contain a
newline if you want the marker to stay on one line.