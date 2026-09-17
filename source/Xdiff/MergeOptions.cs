// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// Optional parameters that control how a three-way merge is computed and how
/// conflicts are formatted. Maps to xdiff's <c>xmparam_t</c>.
/// </summary>
/// <remarks>
/// Note that the indent heuristic is intentionally not exposed: this API does not
/// enable it for the merge path, so internal diffing always runs with the
/// indent heuristic disabled.
/// </remarks>
public sealed record MergeOptions
{
    /// <summary>
    /// Diff algorithm used for the underlying ancestor↔ours and ancestor↔theirs
    /// diffs. Defaults to <see cref="DiffAlgorithm.Myers" />.
    /// </summary>
    public DiffAlgorithm Algorithm { get; init; } = DiffAlgorithm.Myers;

    /// <summary>
    /// Whitespace-equivalence mode applied during line comparison. Defaults to
    /// <see cref="WhitespaceMode.None" />.
    /// </summary>
    public WhitespaceMode Whitespace { get; init; }

    /// <summary>
    /// Conflict-refinement level. Defaults to <see cref="MergeLevel.Zealous" />.
    /// Clamped to <see cref="MergeLevel.Eager" /> when
    /// <see cref="Style" /> is <see cref="MergeStyle.Diff3" /> or
    /// <see cref="MergeStyle.ZealousDiff3" />.
    /// </summary>
    public MergeLevel Level { get; init; } = MergeLevel.Zealous;

    /// <summary>
    /// How to resolve each conflict region. Defaults to
    /// <see cref="MergeFavor.Default" /> (leave conflicts marked).
    /// </summary>
    public MergeFavor Favor { get; init; }

    /// <summary>
    /// Format of conflict markers. Defaults to <see cref="MergeStyle.Merge" />.
    /// </summary>
    public MergeStyle Style { get; init; }

    /// <summary>
    /// Number of characters used to build each conflict marker
    /// (<c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c>, <c>=======</c>, <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c>,
    /// and diff3's <c>|||||||</c>). Defaults to <c>7</c>. Values ≤ <c>0</c>
    /// fall back to <c>7</c>.
    /// </summary>
    public int MarkerSize { get; init; } = 7;

    /// <summary>
    /// Optional label appended to the ancestor marker (<c>|||||||</c>) in
    /// diff3-style output. UTF-8 encoded when emitted; <c>null</c> omits the label.
    /// Ignored for <see cref="MergeStyle.Merge" />.
    /// </summary>
    public string? AncestorLabel { get; init; }

    /// <summary>
    /// Optional label appended to the <c>ours</c> marker
    /// (<c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c>). UTF-8 encoded when emitted;
    /// <c>null</c> omits the label.
    /// </summary>
    public string? OurLabel { get; init; }

    /// <summary>
    /// Optional label appended to the <c>theirs</c> marker
    /// (<c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c>). UTF-8 encoded when emitted;
    /// <c>null</c> omits the label.
    /// </summary>
    public string? TheirLabel { get; init; }
}
