// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// A single line emitted as part of a unified-diff hunk.
/// </summary>
public enum DiffLineKind
{
    /// <summary>An unchanged context line, present in both buffers (rendered with a leading space).</summary>
    Context,

    /// <summary>A line that exists only in the new buffer (rendered with a leading <c>+</c>).</summary>
    Addition,

    /// <summary>A line that exists only in the old buffer (rendered with a leading <c>-</c>).</summary>
    Deletion,
}
