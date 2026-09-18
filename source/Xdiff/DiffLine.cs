// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// A single line emitted as part of a unified-diff hunk.
/// </summary>
/// <param name="Kind">
/// Whether this line is context, an addition, or a deletion.
/// </param>
/// <param name="OldLine">
/// 1-based line number in the old buffer, or <c>0</c> when not applicable
/// (i.e. for <see cref="DiffLineKind.Addition" /> lines).
/// </param>
/// <param name="NewLine">
/// 1-based line number in the new buffer, or <c>0</c> when not applicable
/// (i.e. for <see cref="DiffLineKind.Deletion" /> lines).
/// </param>
/// <param name="Content">
/// The raw line bytes (without the leading <c>+</c>/<c>-</c>/<c> </c>sigil). The
/// slice *literally* aliases the caller's input buffer — no copy is made — so
/// keep the source <see cref="ReadOnlyMemory{T}" /> alive while this line is in
/// use, and note that mutating the source buffer after
/// <see cref="Diff.Compute(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, Xdiff.DiffOptions?)" />
/// is observable through this line.
/// </param>
public readonly record struct DiffLine(
    DiffLineKind Kind,
    int OldLine,
    int NewLine,
    ReadOnlyMemory<byte> Content);
