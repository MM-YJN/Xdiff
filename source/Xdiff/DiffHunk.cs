// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// One contiguous hunk of a unified diff: a header (line ranges and optional
/// function name) followed by the affected lines.
/// </summary>
/// <param name="OldStart">
/// Starting line of the hunk in the old buffer, matching the number emitted after
/// <c>@@ -</c> in unified-diff text. The value is normally 1-based. When
/// <see cref="OldCount" /> is <c>0</c>, it identifies the line before the insertion
/// point and may therefore be <c>0</c> for an insertion at the beginning of the buffer.
/// </param>
/// <param name="OldCount">Number of old-buffer lines covered by the hunk (context + deletions).</param>
/// <param name="NewStart">
/// Starting line of the hunk in the new buffer, matching the number emitted after
/// <c>+</c> in unified-diff text. The value is normally 1-based. When
/// <see cref="NewCount" /> is <c>0</c>, it identifies the line before the deletion
/// point and may therefore be <c>0</c> for a deletion at the beginning of the buffer.
/// </param>
/// <param name="NewCount">Number of new-buffer lines covered by the hunk (context + additions).</param>
/// <param name="FunctionName">
/// Optional function-name annotation captured for this hunk: the undecoded raw bytes
/// that follow <c>@@ ... @@</c> in unified-diff text. Empty when function-name
/// emission is disabled or no matching line has yet been found; a later hunk
/// without a match reuses the most recently matched name. Like
/// <see cref="DiffLine.Content" />, the memory aliases the caller's old input
/// buffer without copying, so keep that buffer alive while this hunk is in use
/// and note that mutating it afterward is observable through this value.
/// </param>
/// <param name="Lines">The context/addition/deletion lines that make up the hunk, in order.</param>
public sealed record DiffHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    ReadOnlyMemory<byte> FunctionName,
    IReadOnlyList<DiffLine> Lines);
