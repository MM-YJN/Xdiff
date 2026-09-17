/*
 *  LibXDiff by Davide Libenzi ( File Differential Library )
 *  Copyright (C) 2003	Davide Libenzi
 *
 *  This library is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 2.1 of the License, or (at your option) any later version.
 *
 *  This library is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 *  Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public
 *  License along with this library; if not, see
 *  <http://www.gnu.org/licenses/>.
 *
 *  Davide Libenzi <davidel@xmailserver.org>
 *
 */

// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

using System.Text;

using Xdiff.Algorithms;
using Xdiff.Core;
using Xdiff.Emit;
using Xdiff.Prepare;
using Xdiff.Util;

namespace Xdiff;

/// <summary>
/// Static facade for computing structured and text unified diffs. Maps to
/// xdiff's <c>xdl_diff</c> entry point.
/// </summary>
public static class Diff
{
    /// <summary>
    /// Computes a structured diff of two UTF-8 (or raw byte) buffers.
    /// </summary>
    /// <param name="oldData">The original buffer. Lines from this buffer appear as <see cref="DiffLineKind.Deletion" /> and context.</param>
    /// <param name="newData">The modified buffer. Lines from this buffer appear as <see cref="DiffLineKind.Addition" /> and context.</param>
    /// <param name="options">Optional diff parameters; <c>null</c> uses default <see cref="DiffOptions" />.</param>
    /// <returns>
    /// A <see cref="DiffResult" /> whose <see cref="DiffLine.Content" /> slices borrow directly from
    /// <paramref name="oldData" /> and <paramref name="newData" />; keep those buffers alive while the
    /// result is in use. Returns a <see cref="DiffResult.IsEmpty" /> result when the buffers are equal.
    /// </returns>
    /// <remarks>
    /// Inputs are taken as <see cref="ReadOnlyMemory{T}" /> (not <see cref="ReadOnlySpan{T}" />) so the
    /// returned <see cref="DiffLine.Content" /> can safely alias the caller's buffer.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DiffOptions.ContextLines" /> or <see cref="DiffOptions.InterHunkLines" /> is negative.
    /// </exception>
    public static DiffResult Compute(ReadOnlyMemory<byte> oldData, ReadOnlyMemory<byte> newData, DiffOptions? options = null)
    {
        DiffOptions opts = options ?? new DiffOptions();
        ValidateOptions(opts);
        (XdfEnv? env, XdChange? script) = Prepare(oldData, newData, opts);

        var sink = new StructuredSink();
        UnifiedDiffEmitter.Emit(env, script, sink, opts);
        return sink.ToResult();
    }

    /// <summary>
    /// Computes and renders a unified diff as a UTF-8-decoded string. Equivalent to
    /// <see cref="UnifiedDiff(ReadOnlySpan{byte}, ReadOnlySpan{byte}, DiffOptions?)" /> after UTF-8-encoding
    /// both inputs.
    /// </summary>
    /// <param name="oldText">The original text.</param>
    /// <param name="newText">The modified text.</param>
    /// <param name="options">Optional diff parameters; <c>null</c> uses default <see cref="DiffOptions" />.</param>
    /// <returns>The unified-diff text, or the empty string when the inputs are equal.</returns>
    /// <remarks>
    /// Output contains hunk headers and line content without Git file headers or repository metadata.
    /// Consumers requiring complete patches must add file framing and verify compatibility with their target tools.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DiffOptions.ContextLines" /> or <see cref="DiffOptions.InterHunkLines" /> is negative.
    /// </exception>
    public static string UnifiedDiff(string oldText, string newText, DiffOptions? options = null)
    {
        DiffOptions opts = options ?? new DiffOptions();
        ValidateOptions(opts);
        byte[] oldBytes = Encoding.UTF8.GetBytes(oldText);
        byte[] newBytes = Encoding.UTF8.GetBytes(newText);
        return Encoding.UTF8.GetString(UnifiedDiff(oldBytes, newBytes, opts));
    }

    /// <summary>
    /// Computes and renders unified-diff hunks directly to a fresh byte buffer without UTF-8 conversion.
    /// Emits hunk headers and prefixed line content. When an emitted input line lacks a trailing
    /// <c>\n</c>, adds a newline and the <c>\ No newline at end of file</c> marker after that line.
    /// </summary>
    /// <param name="oldData">The original buffer.</param>
    /// <param name="newData">The modified buffer.</param>
    /// <param name="options">Optional diff parameters; <c>null</c> uses default <see cref="DiffOptions" />.</param>
    /// <returns>A newly allocated byte array containing the unified-diff text. Empty when the inputs are equal.</returns>
    /// <remarks>
    /// Inputs undergo line-based processing without automatic binary detection.
    /// Output omits Git file headers and repository metadata. Consumers requiring complete patches
    /// must add file framing and verify compatibility with their target tools.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DiffOptions.ContextLines" /> or <see cref="DiffOptions.InterHunkLines" /> is negative.
    /// </exception>
    public static byte[] UnifiedDiff(ReadOnlySpan<byte> oldData, ReadOnlySpan<byte> newData, DiffOptions? options = null)
    {
        DiffOptions opts = options ?? new DiffOptions();
        ValidateOptions(opts);
        (XdfEnv? env, XdChange? script) = Prepare(oldData.ToArray(), newData.ToArray(), opts);

        using var sink = new StringSink();
        UnifiedDiffEmitter.Emit(env, script, sink, opts);
        return sink.ToBytes();
    }

    private static void ValidateOptions(DiffOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(options.ContextLines, nameof(DiffOptions.ContextLines));
        ArgumentOutOfRangeException.ThrowIfNegative(options.InterHunkLines, nameof(DiffOptions.InterHunkLines));
    }

    private static (XdfEnv Env, XdChange? Script) Prepare(ReadOnlyMemory<byte> oldData, ReadOnlyMemory<byte> newData, DiffOptions opts)
    {
        var env = new XdfEnv();
        FilePreparer.PrepareEnv(oldData, newData, opts.Whitespace, opts.Algorithm, env);
        XdChange? script = MyersDiff.Run(env, opts.Algorithm, opts.IndentHeuristic);

        if (opts.IgnoreBlankLines)
        {
            MarkIgnorableLines(script, env, opts.Whitespace);
        }

        return (env, script);
    }

    private static void MarkIgnorableLines(XdChange? script, XdfEnv env, WhitespaceMode ws)
    {
        XdFile xdf1 = env.Xdf1;
        XdFile xdf2 = env.Xdf2;

        for (XdChange? xch = script; xch is not null; xch = xch.Next)
        {
            bool ignore = true;

            for (int i = 0; i < xch.Chg1 && ignore; i++)
            {
                XdRecord rec = xdf1.Recs[xch.I1 + i];
                ignore = RecordMatch.IsBlankLine(xdf1.Data.Span.Slice(rec.Offset, rec.Length), ws);
            }

            for (int i = 0; i < xch.Chg2 && ignore; i++)
            {
                XdRecord rec = xdf2.Recs[xch.I2 + i];
                ignore = RecordMatch.IsBlankLine(xdf2.Data.Span.Slice(rec.Offset, rec.Length), ws);
            }

            xch.Ignore = ignore;
        }
    }
}
