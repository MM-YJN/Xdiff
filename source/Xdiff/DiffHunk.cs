// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// One contiguous hunk of a unified diff: a header (line ranges and optional
/// function name) followed by the affected lines.
/// </summary>
/// <param name="OldStart">
/// 1-based starting line of the hunk in the old buffer. Matches the number emitted
/// after <c>@@ -</c> in unified-diff text. When <see cref="OldCount" /> is <c>0</c>
/// the value is the line before the insertion point (still 1-based; matches git).
/// </param>
/// <param name="OldCount">Number of old-buffer lines covered by the hunk (context + deletions).</param>
/// <param name="NewStart">
/// 1-based starting line of the hunk in the new buffer. Matches the number emitted
/// after <c>+</c> in unified-diff text. When <see cref="NewCount" /> is <c>0</c>
/// the value is the line before the deletion point.
/// </param>
/// <param name="NewCount">Number of new-buffer lines covered by the hunk (context + additions).</param>
/// <param name="FunctionName">
/// Optional function-name annotation captured for this hunk — the raw bytes
/// that follow <c>@@ ... @@</c> in unified-diff text, undecoded. <c>null</c> when
/// function-name emission is disabled or no matching line was found. The
/// buffer is owned by this hunk (a copy, not a borrow from the input).
/// </param>
/// <param name="Lines">The context/addition/deletion lines that make up the hunk, in order.</param>
public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    ReadOnlyMemory<byte>? FunctionName,
    IReadOnlyList<DiffLine> Lines);
