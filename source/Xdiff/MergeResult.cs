// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// The merged buffer and the number of unresolved conflicts produced by
/// <see cref="Merger.Merge(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, MergeOptions?)" />.
/// </summary>
/// <param name="Content">
/// The synthesized merge result as UTF-8 bytes. Always freshly allocated; never
/// aliases the caller's input.
/// </param>
/// <param name="ConflictCount">
/// Number of unresolved conflict regions left in <paramref name="Content" />.
/// Always <c>0</c> when <see cref="MergeOptions.Favor" /> is anything other than
/// <see cref="MergeFavor.Default" />, because favouring resolves every conflict.
/// </param>
public readonly record struct MergeResult(byte[] Content, int ConflictCount)
{
    /// <summary><c>true</c> when <see cref="ConflictCount" /> is greater than zero.</summary>
    public bool HasConflicts => ConflictCount > 0;
}
