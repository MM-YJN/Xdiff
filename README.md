# Xdiff

A managed C# port of the [standalone xdiff library](https://github.com/libgit2/xdiff)
for .NET 10. Xdiff provides structured diffs, unified diff text, and three-way
merges. The porting work was done mainly by AI.

## Features

- Myers, patience, and histogram diff algorithms.
- Structured hunks and lines, or unified output as text or bytes.
- Configurable context, whitespace handling, and indentation heuristics.
- Three-way merges with configurable conflict styles, labels, and resolution.
- String and byte APIs; string overloads use UTF-8 conversion.
- Synchronous, AOT-compatible implementation with no unsafe code, reflection,
  I/O, or process-global mutable state.

## Installation

The library targets .NET 10.

```sh
dotnet add package Xdiff
```

## Quick start

Generate a unified diff:

```csharp
using Xdiff;

string patch = Diff.UnifiedDiff("hello\nworld\n", "hello\nXdiff\n");
Console.Write(patch);
```

```diff
@@ -1,2 +1,2 @@
 hello
-world
+Xdiff
```

Merge independent edits using their common ancestor:

```csharp
using Xdiff;

string merged = Merger.Merge(
    ancestor: "title\nbody\nfooter\n",
    ours:     "new title\nbody\nfooter\n",
    theirs:   "title\nbody\nnew footer\n");

Console.Write(merged); // "new title\nbody\nnew footer\n"
```

Conflicting edits can produce conflict markers. Use the byte overload of
`Merger.Merge` to receive a `MergeResult` with `HasConflicts` and `ConflictCount`.
See the [merge guide](docs/merge.md) for conflict handling.

## Compatibility

Xdiff operates on input buffers. Unified output contains hunks without Git file
headers or repository metadata. It does not load Git configuration, attributes,
or language drivers, and does not perform automatic binary detection or generate
Git binary patches. Byte inputs are still processed as lines.

Selected regression cases are checked against reference fixtures. See
[compatibility and reference testing](docs/overview.md#compatibility-and-reference-testing)
for the supported scope, defaults, and fixture provenance.

## Documentation

- [Overview and API contracts](docs/overview.md)
- [Unified diff output](docs/diff-text.md) and [structured diffs](docs/diff-structured.md)
- [Diff options](docs/diff-options.md)
- [Three-way merges](docs/merge.md) and [merge options](docs/merge-options.md)
- [Recipes](docs/recipes.md)

## Development

Use the .NET SDK selected by [global.json](global.json). From the repository root:

```sh
dotnet restore Xdiff.slnx
dotnet build Xdiff.slnx --no-restore
dotnet test --solution Xdiff.slnx
```

Tests use xUnit v3 on Microsoft Testing Platform. See [AGENTS.md](AGENTS.md) for
repository conventions and focused test commands.

## License and attribution

Xdiff’s library is licensed under the GNU Lesser General Public License, version 2.1 or (at your option) any later version (`LGPL-2.1-or-later`), with applicable third-party notices. See [LICENSE](LICENSE) for the full LGPL text and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for upstream attribution and terms, including the histogram implementation’s Eclipse Distribution License v1.0 (`BSD-3-Clause`) and the `PooledByteBufferWriter` helper’s MIT license.
