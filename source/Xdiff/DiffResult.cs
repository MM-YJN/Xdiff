// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// The complete result of a diff: the ordered list of hunks produced by
/// <see cref="Diff.Compute(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, DiffOptions?)" />.
/// </summary>
public sealed record DiffResult(IReadOnlyList<DiffHunk> Hunks)
{
    /// <summary><c>true</c> when the two inputs were identical and no hunks were emitted.</summary>
    public bool IsEmpty => Hunks.Count == 0;
}
