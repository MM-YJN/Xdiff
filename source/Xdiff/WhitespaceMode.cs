// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff;

/// <summary>
/// Whitespace handling applied while hashing and comparing lines during a diff
/// or three-way merge. Maps to the xdiff <c>XDF_IGNORE_*</c> family of flags.
/// </summary>
/// <remarks>
/// <para>
/// The chosen mode must be passed consistently to both <see cref="Util.Hashing.HashRecord" />
/// and <see cref="Util.RecordMatch.RecordsEqual" /> — hashing and equality stay
/// aligned so the classifier's raw-hash gate (see xdiff port hazard #4) is sound.
/// </para>
/// <para>
/// The flags can be combined where it makes sense (e.g.
/// <see cref="IgnoreChanges" /> | <see cref="IgnoreCrAtEol" />). The classifier checks
/// <see cref="IgnoreAll" /> first, then <see cref="IgnoreChanges" />, then
/// <see cref="IgnoreAtEol" />, then <see cref="IgnoreCrAtEol" />.
/// </para>
/// </remarks>
[Flags]
public enum WhitespaceMode
{
    /// <summary>
    /// No whitespace equivalence: every byte, including all whitespace, participates
    /// in hashing and equality. This is the default.
    /// </summary>
    None = 0,

    /// <summary>
    /// Ignore all whitespace when comparing lines (<c>XDF_IGNORE_WHITESPACE</c>).
    /// Whitespace runs are skipped entirely on both sides; two lines match if their
    /// non-whitespace bytes are identical in order.
    /// </summary>
    IgnoreAll = 1,

    /// <summary>
    /// Treat consecutive whitespace as a single space for comparison, but only when
    /// both sides have whitespace at the same logical position
    /// (<c>XDF_IGNORE_WHITESPACE_CHANGE</c>). Whitespace at end-of-line is ignored
    /// unconditionally.
    /// </summary>
    IgnoreChanges = 2,

    /// <summary>
    /// Ignore whitespace only at end-of-line (<c>XDF_IGNORE_WHITESPACE_AT_EOL</c>).
    /// Leading and inter-token whitespace still participates in comparison; only
    /// the trailing whitespace before the newline is ignored.
    /// </summary>
    IgnoreAtEol = 4,

    /// <summary>
    /// Ignore a single <c>\r</c> immediately preceding the terminating <c>\n</c>
    /// (<c>XDF_IGNORE_CR_AT_EOL</c>), treating <c>\r\n</c> and <c>\n</c> as equivalent.
    /// A lone trailing <c>\r</c> on an incomplete line (no <c>\n</c> terminator) is
    /// still significant, matching xdiff's behaviour.
    /// </summary>
    IgnoreCrAtEol = 8,
}
