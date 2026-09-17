// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// Format of conflict markers emitted for unresolved conflicts. Maps to
/// xdiff's merge style selection.
/// </summary>
public enum MergeStyle
{
    /// <summary>
    /// Standard two-way merge markers: <c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c> ours
    /// <c>=======</c> theirs <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c>.
    /// </summary>
    Merge = 0,

    /// <summary>
    /// Diff3 style: includes the ancestor between <c>|||||||</c> and
    /// <c>=======</c> markers so the consumer can see the original text. Forces
    /// <see cref="MergeLevel" /> to be clamped to at most
    /// <see cref="MergeLevel.Eager" />.
    /// </summary>
    Diff3 = 1,

    /// <summary>
    /// Diff3 with zdiff3-style conflict refinement: common leading/trailing
    /// lines inside each conflict are pulled out of the conflict markers
    /// (git's <c>diff3 --zealous</c>).
    /// </summary>
    ZealousDiff3 = 2,
}
