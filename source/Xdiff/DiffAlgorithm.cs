// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// Selects the diff algorithm used to compute the edit script between two buffers.
/// </summary>
/// <remarks>
/// Maps to xdiff <c>XDF_ALGORITHM_*</c>. All four variants produce a minimal
/// edit script by definition; they differ in how they pick among equally-short scripts
/// and how they handle pathological inputs.
/// </remarks>
public enum DiffAlgorithm
{
    /// <summary>
    /// Default Myers O(ND) algorithm with forward/backward snake heuristics
    /// (<c>XDL_SNAKE_CNT=20</c>, <c>XDL_K_HEUR=4</c>). Fast on typical inputs;
    /// may produce slightly non-minimal output on adversarial cases.
    /// </summary>
    Myers,

    /// <summary>
    /// Myers O(ND) with the heuristic short-circuits disabled (<c>need_min=true</c>).
    /// Slower than <see cref="Myers" /> because it always walks the full diagonal
    /// frontier, but guaranteed to emit a minimum-length script.
    /// </summary>
    Minimal,

    /// <summary>
    /// Patience diff: anchors the script on unique common lines (longest increasing
    /// subsequence) and recurses into the gaps. Produces more human-readable output
    /// on reorderings. Skips the trim/cleanup pre-pass used by Myers.
    /// </summary>
    Patience,

    /// <summary>
    /// Histogram diff: like patience but uses the most-common rare line as the
    /// anchor and falls back to Myers on degenerate regions. Robust against
    /// duplicated lines that confuse patience. Skips the trim/cleanup pre-pass.
    /// </summary>
    Histogram,
}
