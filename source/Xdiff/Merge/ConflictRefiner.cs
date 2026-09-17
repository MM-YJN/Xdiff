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

using Xdiff.Algorithms;
using Xdiff.Core;
using Xdiff.Prepare;
using Xdiff.Util;

namespace Xdiff.Merge;

internal static class ConflictRefiner
{
    internal static void RefineZdiff3Conflicts(
        XdfEnv xe1,
        XdfEnv xe2,
        XdMerge? head,
        WhitespaceMode ws)
    {
        ReadOnlySpan<byte> data1 = xe1.Xdf2.Data.Span;
        XdRecord[] recs1 = xe1.Xdf2.Recs;
        ReadOnlySpan<byte> data2 = xe2.Xdf2.Data.Span;
        XdRecord[] recs2 = xe2.Xdf2.Recs;

        for (XdMerge? m = head; m is not null; m = m.Next)
        {
            if (m.Mode != 0)
            {
                continue;
            }

            while (m.Chg1 > 0 && m.Chg2 > 0 &&
                   RecordsMatch(data1, recs1[m.I1], data2, recs2[m.I2], ws))
            {
                m.Chg1--;
                m.Chg2--;
                m.I1++;
                m.I2++;
            }

            while (m.Chg1 > 0 && m.Chg2 > 0 &&
                   RecordsMatch(data1, recs1[m.I1 + m.Chg1 - 1], data2, recs2[m.I2 + m.Chg2 - 1], ws))
            {
                m.Chg1--;
                m.Chg2--;
            }
        }
    }

    internal static void RefineConflicts(
        XdfEnv xe1,
        XdfEnv xe2,
        XdMerge? head,
        WhitespaceMode ws,
        DiffAlgorithm alg)
    {
        for (XdMerge? m = head; m is not null; m = m.Next)
        {
            if (m.Mode != 0)
            {
                continue;
            }

            if (m.Chg1 == 0 || m.Chg2 == 0)
            {
                continue;
            }

            int origI1 = m.I1;
            int origI2 = m.I2;

            ReadOnlyMemory<byte> sub1 = FallBackDiff.ExtractSubBuffer(xe1.Xdf2, origI1 + 1, m.Chg1);
            ReadOnlyMemory<byte> sub2 = FallBackDiff.ExtractSubBuffer(xe2.Xdf2, origI2 + 1, m.Chg2);

            var subEnv = new XdfEnv { Whitespace = ws };
            FilePreparer.PrepareEnv(sub1, sub2, ws, alg, subEnv);
            XdChange? xscr = MyersDiff.Run(subEnv, alg, false);

            if (xscr is null)
            {
                m.Mode = 4;
                continue;
            }

            m.I1 = xscr.I1 + origI1;
            m.Chg1 = xscr.Chg1;
            m.I2 = xscr.I2 + origI2;
            m.Chg2 = xscr.Chg2;

            while (xscr.Next is not null)
            {
                xscr = xscr.Next;
                var m2 = new XdMerge
                {
                    Mode = 0,
                    Next = m.Next,
                    I1 = xscr.I1 + origI1,
                    Chg1 = xscr.Chg1,
                    I2 = xscr.I2 + origI2,
                    Chg2 = xscr.Chg2
                };
                m.Next = m2;
                m = m2;
            }
        }
    }

    internal static int SimplifyNonConflicts(XdfEnv xe1, XdMerge? head, bool simplifyIfNoAlnum)
    {
        int result = 0;
        XdMerge? m = head;

        if (m is null)
        {
            return result;
        }

        while (true)
        {
            XdMerge? nextM = m.Next;

            if (nextM is null)
            {
                return result;
            }

            int begin = m.I1 + m.Chg1;
            int end = nextM.I1;

            if (m.Mode != 0 || nextM.Mode != 0 ||
                (end - begin > 3 &&
                 (!simplifyIfNoAlnum || LinesContainAlnum(xe1, begin, end - begin))))
            {
                m = nextM;
            }
            else
            {
                result++;
                MergeTwoConflicts(m);
            }
        }
    }

    private static void MergeTwoConflicts(XdMerge m)
    {
        XdMerge? nextM = m.Next;
        Debug.Assert(nextM is not null, "nextM is non-null when merging two adjacent conflicts");
        m.Chg1 = nextM.I1 + nextM.Chg1 - m.I1;
        m.Chg2 = nextM.I2 + nextM.Chg2 - m.I2;
        m.Next = nextM.Next;
    }

    private static bool RecordsMatch(
        ReadOnlySpan<byte> data1,
        XdRecord rec1,
        ReadOnlySpan<byte> data2,
        XdRecord rec2,
        WhitespaceMode ws)
    {
        return RecordMatch.RecordsEqual(
            data1.Slice(rec1.Offset, rec1.Length),
            data2.Slice(rec2.Offset, rec2.Length),
            ws);
    }

    private static bool LinesContainAlnum(XdfEnv xe, int i, int chg)
    {
        XdFile xdf = xe.Xdf2;
        ReadOnlySpan<byte> data = xdf.Data.Span;
        XdRecord[] recs = xdf.Recs;

        for (; chg > 0; chg--, i++)
        {
            XdRecord rec = recs[i];
            if (LineContainsAlnum(data.Slice(rec.Offset, rec.Length)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LineContainsAlnum(ReadOnlySpan<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            byte c = span[i];
            if (c is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9')
            {
                return true;
            }
        }

        return false;
    }
}
