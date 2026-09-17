// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// Resolution policy applied to each conflict region after the three-way merge
/// has classified regions. Maps to xdiff's <c>XDL_MERGE_FAVOR_*</c>.
/// </summary>
/// <remarks>
/// When set to anything other than <see cref="Default" />, every conflict is
/// resolved in favour of the chosen side and <see cref="MergeResult.ConflictCount" />
/// is reported as <c>0</c>.
/// </remarks>
public enum MergeFavor
{
    /// <summary>
    /// Leave conflicts in place with conflict markers (normal merge).
    /// </summary>
    Default = 0,

    /// <summary>Resolve all conflicts by taking the <c>ours</c> side.</summary>
    Ours = 1,

    /// <summary>Resolve all conflicts by taking the <c>theirs</c> side.</summary>
    Theirs = 2,

    /// <summary>
    /// Resolve all conflicts by concatenating both sides (ours followed by theirs).
    /// </summary>
    Union = 3,
}
