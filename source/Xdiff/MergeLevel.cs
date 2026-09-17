// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// How aggressively to refine raw conflict regions into smaller, more accurate
/// conflicts. Maps to xdiff's <c>XDL_MERGE_ZEALOUS_*</c> levels.
/// </summary>
/// <remarks>
/// Levels are cumulative: <see cref="Eager" /> adds conflict refinement on top
/// of <see cref="Minimal" />, <see cref="Zealous" /> adds non-conflict
/// simplification, and <see cref="ZealousAlnum" /> tightens the simplification
/// rule. Diff3 styles (<see cref="MergeStyle.Diff3" /> /
/// <see cref="MergeStyle.ZealousDiff3" />) clamp this to
/// <see cref="Eager" /> at most because their conflict refinement takes a
/// different path.
/// </remarks>
public enum MergeLevel
{
    /// <summary>
    /// No refinement: every overlapping change becomes one big conflict region.
    /// Fastest but produces the coarsest conflicts.
    /// </summary>
    Minimal = 0,

    /// <summary>
    /// Recursively re-diff each conflict region (ours vs theirs) and split it
    /// into independent conflicts and agreed-upon regions.
    /// </summary>
    Eager = 1,

    /// <summary>
    /// Eager plus simplification: adjacent conflicts separated by a small
    /// gap (≤3 unchanged lines) are merged back into a single conflict.
    /// </summary>
    Zealous = 2,

    /// <summary>
    /// Like <see cref="Zealous" /> but the simplification step only fires when
    /// the gap between adjacent conflicts contains no alphanumeric (C-locale
    /// ASCII) bytes. Mirrors git's <c>--zealous-alnum</c> semantics.
    /// </summary>
    ZealousAlnum = 3,
}
