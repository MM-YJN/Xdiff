/*
 *  LibXDiff by Davide Libenzi ( File Differential Library )
 *  Copyright (C) 2003-2006 Davide Libenzi, Johannes E. Schindelin
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

using System.Buffers;
using System.Text;

using Xdiff.Algorithms;
using Xdiff.Core;
using Xdiff.Merge;
using Xdiff.Prepare;
using Xdiff.Util;

namespace Xdiff;

/// <summary>
/// Static facade for performing three-way merges. Maps to xdiff's
/// <c>xdl_merge</c> entry point.
/// </summary>
public static class Merger
{
    /// <summary>
    /// Performs a three-way merge of <paramref name="ours" /> and <paramref name="theirs" /> against
    /// the common <paramref name="ancestor" />, returning a freshly synthesized merged buffer with
    /// standard conflict markers for any unresolved regions.
    /// </summary>
    /// <param name="ancestor">The common-ancestor buffer (a.k.a. base / ours~1).</param>
    /// <param name="ours">Our side of the merge.</param>
    /// <param name="theirs">Their side of the merge.</param>
    /// <param name="options">Optional merge parameters; <c>null</c> uses default <see cref="MergeOptions" />.</param>
    /// <returns>
    /// A <see cref="MergeResult" /> whose <see cref="MergeResult.Content" /> is a fresh
    /// <see cref="byte" />[] (it never aliases the caller's input). When one side is identical to
    /// the ancestor, the other side is copied verbatim (fast path). <see cref="MergeResult.ConflictCount" />
    /// is <c>0</c> when <see cref="MergeOptions.Favor" /> is anything other than
    /// <see cref="MergeFavor.Default" />.
    /// </returns>
    /// <remarks>
    /// Inputs are accepted as <see cref="ReadOnlyMemory{T}" /> and borrowed for the duration of
    /// the call only; the
    /// method does not retain or take ownership of them. Keep input memory valid and unmodified
    /// until the call returns. The output buffer is synthesized from
    /// possibly-conflicting regions and never borrows from the caller's input. Binary detection
    /// (NUL byte in first 8000 bytes) is the caller's responsibility.
    /// </remarks>
    public static MergeResult Merge(
        ReadOnlyMemory<byte> ancestor,
        ReadOnlyMemory<byte> ours,
        ReadOnlyMemory<byte> theirs,
        MergeOptions? options = null)
    {
        MergeOptions opts = options ?? new MergeOptions();

        var xe1 = new XdfEnv();
        FilePreparer.PrepareEnv(ancestor, ours, opts.Whitespace, opts.Algorithm, xe1);
        XdChange? xscr1 = MyersDiff.Run(xe1, opts.Algorithm, false);

        var xe2 = new XdfEnv();
        FilePreparer.PrepareEnv(ancestor, theirs, opts.Whitespace, opts.Algorithm, xe2);
        XdChange? xscr2 = MyersDiff.Run(xe2, opts.Algorithm, false);

        if (xscr1 is null)
        {
            return new MergeResult(theirs.ToArray(), 0);
        }

        if (xscr2 is null)
        {
            return new MergeResult(ours.ToArray(), 0);
        }

        (XdMerge? head, int conflictCount) = ThreeWayMerger.DoMerge(xe1, xscr1, xe2, xscr2, opts);

        byte[]? name1 = opts.OurLabel is not null ? Encoding.UTF8.GetBytes(opts.OurLabel) : null;
        byte[]? name2 = opts.TheirLabel is not null ? Encoding.UTF8.GetBytes(opts.TheirLabel) : null;
        byte[]? ancestorName = opts.AncestorLabel is not null ? Encoding.UTF8.GetBytes(opts.AncestorLabel) : null;

        using var writer = new PooledByteBufferWriter();
        MergeBufferBuilder.Fill(writer,
            xe1, xe2, head, opts.Style, opts.MarkerSize,
            name1, name2, ancestorName);

        return new MergeResult(writer.WrittenSpan.ToArray(), conflictCount);
    }

    /// <summary>
    /// Performs a three-way merge of <paramref name="ours" /> and <paramref name="theirs" /> against
    /// the common <paramref name="ancestor" />, writing the merged content with standard conflict
    /// markers for any unresolved regions to <paramref name="writer" />.
    /// </summary>
    /// <param name="writer">The <see cref="IBufferWriter{T}" /> to which the merged content is written.</param>
    /// <param name="ancestor">The common-ancestor buffer (a.k.a. base / ours~1).</param>
    /// <param name="ours">Our side of the merge.</param>
    /// <param name="theirs">Their side of the merge.</param>
    /// <param name="options">Optional merge parameters; <c>null</c> uses default <see cref="MergeOptions" />.</param>
    /// <returns>
    /// The number of unresolved conflicts in the merged output. Always <c>0</c> when
    /// <see cref="MergeOptions.Favor" /> is anything other than <see cref="MergeFavor.Default" />.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Input ownership</b>: <paramref name="ancestor" />, <paramref name="ours" />, and
    /// <paramref name="theirs" /> are borrowed for the duration of the call only — the method
    /// neither retains nor disposes them, and the caller remains their owner. Keep input memory valid
    /// and unmodified, including by output writes, until the call returns. When one side is
    /// identical to the ancestor, the other side's bytes are written through verbatim (fast path).
    /// </para>
    /// <para>
    /// <b>Output ownership</b>: <paramref name="writer" /> is supplied and owned by the caller;
    /// the method appends the merged bytes and never resets, clears, consumes, or disposes it.
    /// Every byte is written before the method returns — the caller may inspect the concrete
    /// writer's written views (e.g. <c>WrittenSpan</c> on <see cref="ArrayBufferWriter{T}" />) immediately
    /// after, and remains responsible for any cleanup or lifetime the writer requires. The writer
    /// must not be written to concurrently from another thread for the duration of the call.
    /// Writer exceptions propagate; already appended bytes are not rolled back.
    /// </para>
    /// </remarks>
    public static int Merge(
        IBufferWriter<byte> writer,
        ReadOnlyMemory<byte> ancestor,
        ReadOnlyMemory<byte> ours,
        ReadOnlyMemory<byte> theirs,
        MergeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);

        MergeOptions opts = options ?? new MergeOptions();

        var xe1 = new XdfEnv();
        FilePreparer.PrepareEnv(ancestor, ours, opts.Whitespace, opts.Algorithm, xe1);
        XdChange? xscr1 = MyersDiff.Run(xe1, opts.Algorithm, false);

        var xe2 = new XdfEnv();
        FilePreparer.PrepareEnv(ancestor, theirs, opts.Whitespace, opts.Algorithm, xe2);
        XdChange? xscr2 = MyersDiff.Run(xe2, opts.Algorithm, false);

        if (xscr1 is null)
        {
            writer.Write(theirs.Span);
            return 0;
        }

        if (xscr2 is null)
        {
            writer.Write(ours.Span);
            return 0;
        }

        (XdMerge? head, int conflictCount) = ThreeWayMerger.DoMerge(xe1, xscr1, xe2, xscr2, opts);

        byte[]? name1 = opts.OurLabel is not null ? Encoding.UTF8.GetBytes(opts.OurLabel) : null;
        byte[]? name2 = opts.TheirLabel is not null ? Encoding.UTF8.GetBytes(opts.TheirLabel) : null;
        byte[]? ancestorName = opts.AncestorLabel is not null ? Encoding.UTF8.GetBytes(opts.AncestorLabel) : null;

        MergeBufferBuilder.Fill(writer,
            xe1, xe2, head, opts.Style, opts.MarkerSize,
            name1, name2, ancestorName);

        return conflictCount;
    }

    /// <summary>
    /// UTF-8 string convenience overload of
    /// <see cref="Merge(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, MergeOptions?)" />.
    /// Encodes the inputs as UTF-8, performs the merge, then UTF-8-decodes the result.
    /// </summary>
    /// <param name="ancestor">The common-ancestor text.</param>
    /// <param name="ours">Our side of the merge.</param>
    /// <param name="theirs">Their side of the merge.</param>
    /// <param name="options">Optional merge parameters; <c>null</c> uses default <see cref="MergeOptions" />.</param>
    /// <returns>The merged UTF-8 text (possibly containing conflict markers).</returns>
    public static string Merge(
        string ancestor,
        string ours,
        string theirs,
        MergeOptions? options = null)
    {
        byte[] ancestorBytes = Encoding.UTF8.GetBytes(ancestor);
        byte[] ourBytes = Encoding.UTF8.GetBytes(ours);
        byte[] theirBytes = Encoding.UTF8.GetBytes(theirs);
        MergeResult result = Merge(ancestorBytes, ourBytes, theirBytes, options);
        return Encoding.UTF8.GetString(result.Content);
    }
}
