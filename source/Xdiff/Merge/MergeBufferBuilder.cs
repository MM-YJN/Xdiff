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

using System.Diagnostics;

using Xdiff.Core;

namespace Xdiff.Merge;

internal static class MergeBufferBuilder
{
    private const int DefaultConflictMarkerSize = 7;

    internal static byte[] Fill(
        XdfEnv xe1,
        XdfEnv xe2,
        XdMerge? head,
        MergeStyle style,
        int markerSize,
        byte[]? name1,
        byte[]? name2,
        byte[]? ancestorName)
    {
        int size = FillLoop(xe1, xe2, head, style, markerSize, name1, name2, ancestorName, default, false);
        byte[] buffer = new byte[size];
        FillLoop(xe1, xe2, head, style, markerSize, name1, name2, ancestorName, buffer, true);
        return buffer;
    }

    private static int FillLoop(
        XdfEnv xe1,
        XdfEnv xe2,
        XdMerge? head,
        MergeStyle style,
        int markerSize,
        byte[]? name1,
        byte[]? name2,
        byte[]? ancestorName,
        Span<byte> dest,
        bool writing)
    {
        int size = 0;
        int i = 0;

        for (XdMerge? m = head; m is not null; m = m.Next)
        {
            if (m.Mode == 0)
            {
                size = FillConflictHunk(xe1, xe2, name1, name2, ancestorName,
                    size, i, style, m, dest, writing, markerSize);
            }
            else if ((m.Mode & 3) != 0)
            {
                size += CopyRecs(xe1.Xdf2, i, m.I1 - i, false, false, dest, size, writing);

                if ((m.Mode & 1) != 0)
                {
                    bool needsCr = IsCrNeeded(xe1, xe2, m);
                    size += CopyRecs(xe1.Xdf2, m.I1, m.Chg1, needsCr, (m.Mode & 2) != 0, dest, size, writing);
                }

                if ((m.Mode & 2) != 0)
                {
                    size += CopyRecs(xe2.Xdf2, m.I2, m.Chg2, false, false, dest, size, writing);
                }
            }
            else
            {
                continue;
            }

            i = m.I1 + m.Chg1;
        }

        size += CopyRecs(xe1.Xdf2, i, xe1.Xdf2.Nrec - i, false, false, dest, size, writing);
        return size;
    }

    private static int FillConflictHunk(
        XdfEnv xe1,
        XdfEnv xe2,
        byte[]? name1,
        byte[]? name2,
        byte[]? ancestorName,
        int size,
        int i,
        MergeStyle style,
        XdMerge m,
        Span<byte> dest,
        bool writing,
        int markerSize)
    {
        int marker1Size = name1 is not null ? name1.Length + 1 : 0;
        int marker2Size = name2 is not null ? name2.Length + 1 : 0;
        int marker3Size = ancestorName is not null ? ancestorName.Length + 1 : 0;
        bool needsCr = IsCrNeeded(xe1, xe2, m);

        if (markerSize <= 0)
        {
            markerSize = DefaultConflictMarkerSize;
        }

        size += CopyRecs(xe1.Xdf2, i, m.I1 - i, false, false, dest, size, writing);

        size += EmitMarker(dest, size, writing, (byte)'<', markerSize, name1, marker1Size, needsCr);

        size += CopyRecs(xe1.Xdf2, m.I1, m.Chg1, needsCr, true, dest, size, writing);

        if (style is MergeStyle.Diff3 or MergeStyle.ZealousDiff3)
        {
            size += EmitMarker(dest, size, writing, (byte)'|', markerSize, ancestorName, marker3Size, needsCr);
            size += CopyRecs(xe1.Xdf1, m.I0, m.Chg0, needsCr, true, dest, size, writing);
        }

        size += EmitMarker(dest, size, writing, (byte)'=', markerSize, null, 0, needsCr);

        size += CopyRecs(xe2.Xdf2, m.I2, m.Chg2, needsCr, true, dest, size, writing);

        size += EmitMarker(dest, size, writing, (byte)'>', markerSize, name2, marker2Size, needsCr);

        return size;
    }

    private static int EmitMarker(
        Span<byte> dest,
        int offset,
        bool writing,
        byte fill,
        int markerSize,
        byte[]? label,
        int labelSize,
        bool needsCr)
    {
        int size = 0;

        if (writing)
        {
            dest.Slice(offset, markerSize).Fill(fill);
        }

        size += markerSize;

        if (labelSize > 0)
        {
            Debug.Assert(label is not null, "label is set when labelSize > 0");
            if (writing)
            {
                dest[offset + size] = (byte)' ';
                label.AsSpan(0, labelSize - 1).CopyTo(dest.Slice(offset + size + 1, labelSize - 1));
            }

            size += labelSize;
        }

        if (writing)
        {
            if (needsCr)
            {
                dest[offset + size] = (byte)'\r';
            }

            dest[offset + size + (needsCr ? 1 : 0)] = (byte)'\n';
        }

        size += needsCr ? 2 : 1;

        return size;
    }

    private static int CopyRecs(
        XdFile xdf,
        int i,
        int count,
        bool needsCr,
        bool addNl,
        Span<byte> dest,
        int offset,
        bool writing)
    {
        if (count < 1)
        {
            return 0;
        }

        ReadOnlySpan<byte> data = xdf.Data.Span;
        XdRecord[] recs = xdf.Recs;
        int size = 0;

        for (int j = 0; j < count; j++)
        {
            XdRecord rec = recs[i + j];

            if (writing)
            {
                data.Slice(rec.Offset, rec.Length).CopyTo(dest.Slice(offset + size, rec.Length));
            }

            size += rec.Length;
        }

        if (addNl)
        {
            XdRecord lastRec = recs[i + count - 1];
            int lastLen = lastRec.Length;

            if (lastLen == 0 || data[lastRec.Offset + lastLen - 1] != '\n')
            {
                if (needsCr)
                {
                    if (writing)
                    {
                        dest[offset + size] = (byte)'\r';
                    }

                    size++;
                }

                if (writing)
                {
                    dest[offset + size] = (byte)'\n';
                }

                size++;
            }
        }

        return size;
    }

    private static bool IsCrNeeded(XdfEnv xe1, XdfEnv xe2, XdMerge m)
    {
        int needsCr = IsEolCrlf(xe1.Xdf2, m.I1 != 0 ? m.I1 - 1 : 0);

        if (needsCr != 0)
        {
            needsCr = IsEolCrlf(xe2.Xdf2, m.I2 != 0 ? m.I2 - 1 : 0);
        }

        if (needsCr != 0)
        {
            needsCr = IsEolCrlf(xe1.Xdf1, 0);
        }

        return needsCr > 0;
    }

    private static int IsEolCrlf(XdFile file, int i)
    {
        ReadOnlySpan<byte> data = file.Data.Span;
        XdRecord[] recs = file.Recs;

        if (i < file.Nrec - 1)
        {
            XdRecord rec = recs[i];
            return rec.Length > 1 && data[rec.Offset + rec.Length - 2] == '\r' ? 1 : 0;
        }

        if (file.Nrec == 0)
        {
            return -1;
        }

        {
            XdRecord rec = recs[i];
            if (rec.Length > 0 && data[rec.Offset + rec.Length - 1] == '\n')
            {
                return rec.Length > 1 && data[rec.Offset + rec.Length - 2] == '\r' ? 1 : 0;
            }
        }

        if (i == 0)
        {
            return -1;
        }

        {
            XdRecord rec = recs[i - 1];
            return rec.Length > 1 && data[rec.Offset + rec.Length - 2] == '\r' ? 1 : 0;
        }
    }
}
