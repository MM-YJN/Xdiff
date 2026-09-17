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
using Xdiff.Util;

namespace Xdiff.Merge;

internal static class ThreeWayMerger
{
    internal static (XdMerge? Head, int ConflictCount) DoMerge(
        XdfEnv xe1,
        XdChange? xscr1,
        XdfEnv xe2,
        XdChange? xscr2,
        MergeOptions opts)
    {
        MergeLevel level = opts.Level;
        MergeStyle style = opts.Style;
        MergeFavor favor = opts.Favor;
        WhitespaceMode ws = opts.Whitespace;
        DiffAlgorithm alg = opts.Algorithm;

        if (style is MergeStyle.Diff3 or MergeStyle.ZealousDiff3)
        {
            if (level > MergeLevel.Eager)
            {
                level = MergeLevel.Eager;
            }
        }

        XdMerge? head = null;
        XdMerge? tail = null;

        while (xscr1 is not null && xscr2 is not null)
        {
            if (xscr1.I1 + xscr1.Chg1 < xscr2.I1)
            {
                int i0 = xscr1.I1;
                int i1 = xscr1.I2;
                int i2 = xscr2.I2 - xscr2.I1 + xscr1.I1;
                int chg0 = xscr1.Chg1;
                int chg1 = xscr1.Chg2;
                int chg2 = xscr1.Chg1;
                AppendMerge(ref head, ref tail, 1, i0, chg0, i1, chg1, i2, chg2);
                xscr1 = xscr1.Next;
                continue;
            }

            if (xscr2.I1 + xscr2.Chg1 < xscr1.I1)
            {
                int i0 = xscr2.I1;
                int i1 = xscr1.I2 - xscr1.I1 + xscr2.I1;
                int i2 = xscr2.I2;
                int chg0 = xscr2.Chg1;
                int chg1 = xscr2.Chg1;
                int chg2 = xscr2.Chg2;
                AppendMerge(ref head, ref tail, 2, i0, chg0, i1, chg1, i2, chg2);
                xscr2 = xscr2.Next;
                continue;
            }

            if (level == MergeLevel.Minimal || xscr1.I1 != xscr2.I1 ||
                xscr1.Chg1 != xscr2.Chg1 || xscr1.Chg2 != xscr2.Chg2 ||
                !CmpLines(xe1, xscr1.I2, xe2, xscr2.I2, xscr1.Chg2, ws))
            {
                int off = xscr1.I1 - xscr2.I1;
                int ffo = off + xscr1.Chg1 - xscr2.Chg1;

                int i0 = xscr1.I1;
                int i1 = xscr1.I2;
                int i2 = xscr2.I2;

                if (off > 0)
                {
                    i0 -= off;
                    i1 -= off;
                }
                else
                {
                    i2 += off;
                }

                int chg0 = xscr1.I1 + xscr1.Chg1 - i0;
                int chg1 = xscr1.I2 + xscr1.Chg2 - i1;
                int chg2 = xscr2.I2 + xscr2.Chg2 - i2;

                if (ffo < 0)
                {
                    chg0 -= ffo;
                    chg1 -= ffo;
                }
                else
                {
                    chg2 += ffo;
                }

                AppendMerge(ref head, ref tail, 0, i0, chg0, i1, chg1, i2, chg2);
            }

            int end1 = xscr1.I1 + xscr1.Chg1;
            int end2 = xscr2.I1 + xscr2.Chg1;

            if (end1 >= end2)
            {
                xscr2 = xscr2.Next;
            }

            if (end2 >= end1)
            {
                xscr1 = xscr1.Next;
            }
        }

        while (xscr1 is not null)
        {
            int i0 = xscr1.I1;
            int i1 = xscr1.I2;
            int i2 = xscr1.I1 + xe2.Xdf2.Nrec - xe2.Xdf1.Nrec;
            int chg0 = xscr1.Chg1;
            int chg1 = xscr1.Chg2;
            int chg2 = xscr1.Chg1;
            AppendMerge(ref head, ref tail, 1, i0, chg0, i1, chg1, i2, chg2);
            xscr1 = xscr1.Next;
        }

        while (xscr2 is not null)
        {
            int i0 = xscr2.I1;
            int i1 = xscr2.I1 + xe1.Xdf2.Nrec - xe1.Xdf1.Nrec;
            int i2 = xscr2.I2;
            int chg0 = xscr2.Chg1;
            int chg1 = xscr2.Chg1;
            int chg2 = xscr2.Chg2;
            AppendMerge(ref head, ref tail, 2, i0, chg0, i1, chg1, i2, chg2);
            xscr2 = xscr2.Next;
        }

        if (style == MergeStyle.ZealousDiff3)
        {
            ConflictRefiner.RefineZdiff3Conflicts(xe1, xe2, head, ws);
        }
        else if (level >= MergeLevel.Zealous)
        {
            ConflictRefiner.RefineConflicts(xe1, xe2, head, ws, alg);
            ConflictRefiner.SimplifyNonConflicts(xe1, head, level > MergeLevel.Zealous);
        }

        ApplyFavor(head, favor);
        int conflictCount = CountConflicts(head);
        return (head, conflictCount);
    }

    internal static int CountConflicts(XdMerge? head)
    {
        int count = 0;
        for (XdMerge? m = head; m is not null; m = m.Next)
        {
            if (m.Mode == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static void ApplyFavor(XdMerge? head, MergeFavor favor)
    {
        if (favor == MergeFavor.Default)
        {
            return;
        }

        int favorVal = (int)favor;

        for (XdMerge? m = head; m is not null; m = m.Next)
        {
            if (m.Mode == 0)
            {
                m.Mode = favorVal;
            }
        }
    }

    private static void AppendMerge(
        ref XdMerge? head,
        ref XdMerge? tail,
        int mode,
        int i0, int chg0,
        int i1, int chg1,
        int i2, int chg2)
    {
        var m = new XdMerge
        {
            Mode = mode,
            I0 = i0,
            Chg0 = chg0,
            I1 = i1,
            Chg1 = chg1,
            I2 = i2,
            Chg2 = chg2
        };

        if (head is not null)
        {
            XdMerge? t = tail;
            Debug.Assert(t is not null, "tail is non-null when head is non-null");
            if (i1 <= t.I1 + t.Chg1 || i2 <= t.I2 + t.Chg2)
            {
                if (mode != t.Mode)
                {
                    t.Mode = 0;
                }

                t.Chg0 = i0 + chg0 - t.I0;
                t.Chg1 = i1 + chg1 - t.I1;
                t.Chg2 = i2 + chg2 - t.I2;
                return;
            }

            t.Next = m;
        }
        else
        {
            head = m;
        }

        tail = m;
    }

    private static bool CmpLines(XdfEnv xe1, int i1, XdfEnv xe2, int i2, int lineCount, WhitespaceMode ws)
    {
        ReadOnlySpan<byte> data1 = xe1.Xdf2.Data.Span;
        XdRecord[] recs1 = xe1.Xdf2.Recs;
        ReadOnlySpan<byte> data2 = xe2.Xdf2.Data.Span;
        XdRecord[] recs2 = xe2.Xdf2.Recs;

        for (int i = 0; i < lineCount; i++)
        {
            XdRecord r1 = recs1[i1 + i];
            XdRecord r2 = recs2[i2 + i];

            if (!RecordMatch.RecordsEqual(
                    data1.Slice(r1.Offset, r1.Length),
                    data2.Slice(r2.Offset, r2.Length),
                    ws))
            {
                return false;
            }
        }

        return true;
    }
}
