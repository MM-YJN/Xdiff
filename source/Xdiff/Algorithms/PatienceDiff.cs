/*
 *  LibXDiff by Davide Libenzi ( File Differential Library )
 *  Copyright (C) 2003-2016 Davide Libenzi, Johannes E. Schindelin
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

using Xdiff.Core;

namespace Xdiff.Algorithms;

internal static class PatienceDiff
{
    private const int NonUnique = int.MaxValue;

    internal static void Diff(XdfEnv env) => PatienceDiffRecursive(env, 1, env.Xdf1.Nrec, 1, env.Xdf2.Nrec);

    private static void PatienceDiffRecursive(
        XdfEnv env, int line1, int count1, int line2, int count2)
    {
        if (count1 == 0)
        {
            while (count2-- > 0)
            {
                env.Xdf2.Rchg[line2++] = true;
            }

            return;
        }

        if (count2 == 0)
        {
            while (count1-- > 0)
            {
                env.Xdf1.Rchg[line1++] = true;
            }

            return;
        }

        var map = new PatienceMap(env);
        FillHashmap(map, line1, count1, line2, count2);

        if (!map.HasMatches)
        {
            while (count1-- > 0)
            {
                env.Xdf1.Rchg[line1++] = true;
            }

            while (count2-- > 0)
            {
                env.Xdf2.Rchg[line2++] = true;
            }

            return;
        }

        PatienceEntry? first = FindLongestCommonSequence(map);
        if (first is not null)
        {
            WalkCommonSequence(env, first, line1, count1, line2, count2);
        }
        else
        {
            FallBackDiff.Diff(env, line1, count1, line2, count2);
        }
    }

    private static void FillHashmap(
        PatienceMap map, int line1, int count1, int line2, int count2)
    {
        long[] ha1 = map.Env.Xdf1.Ha;
        for (int i = 0; i < count1; i++)
        {
            map.InsertRecord(line1 + i, pass2: false, ha1[line1 + i - 1]);
        }

        long[] ha2 = map.Env.Xdf2.Ha;
        for (int i = 0; i < count2; i++)
        {
            map.InsertRecord(line2 + i, pass2: true, ha2[line2 + i - 1]);
        }
    }

    private static int BinarySearch(
        PatienceEntry?[] sequence, int longest, PatienceEntry entry)
    {
        int left = -1;
        int right = longest;

        while (left + 1 < right)
        {
            int middle = left + (right - left) / 2;
            if (sequence[middle]!.Line2 > entry.Line2)
            {
                right = middle;
            }
            else
            {
                left = middle;
            }
        }

        return left;
    }

    private static PatienceEntry? FindLongestCommonSequence(PatienceMap map)
    {
        // Not pooled: a reference array would need clearing on return to stop
        // the pool from rooting the PatienceEntry chain — not worth it.
        var sequence = new PatienceEntry?[map.Count];

        int longest = 0;

        foreach (PatienceEntry entry in map.Entries)
        {
            if (entry.Line2 is 0 or NonUnique)
            {
                continue;
            }

            int i = BinarySearch(sequence, longest, entry);
            entry.Previous = i < 0 ? null : sequence[i];
            i++;
            sequence[i] = entry;
            if (i == longest)
            {
                longest++;
            }
        }

        if (longest == 0)
        {
            return null;
        }

        PatienceEntry last = sequence[longest - 1]!;
        last.Next = null;
        while (last.Previous is not null)
        {
            last.Previous.Next = last;
            last = last.Previous;
        }

        return last;
    }

    private static void WalkCommonSequence(
        XdfEnv env, PatienceEntry? first, int line1, int count1, int line2, int count2)
    {
        int end1 = line1 + count1;
        int end2 = line2 + count2;

        while (true)
        {
            int next1;
            int next2;

            if (first is not null)
            {
                next1 = first.Line1;
                next2 = first.Line2;
                while (next1 > line1 && next2 > line2 && Match(env, next1 - 1, next2 - 1))
                {
                    next1--;
                    next2--;
                }
            }
            else
            {
                next1 = end1;
                next2 = end2;
            }

            while (line1 < next1 && line2 < next2 && Match(env, line1, line2))
            {
                line1++;
                line2++;
            }

            if (next1 > line1 || next2 > line2)
            {
                PatienceDiffRecursive(env, line1, next1 - line1, line2, next2 - line2);
            }

            if (first is null)
            {
                return;
            }

            while (first.Next is not null &&
                   first.Next.Line1 == first.Line1 + 1 &&
                   first.Next.Line2 == first.Line2 + 1)
            {
                first = first.Next;
            }

            line1 = first.Line1 + 1;
            line2 = first.Line2 + 1;

            first = first.Next;
        }
    }

    private static bool Match(XdfEnv env, int line1, int line2) => env.Xdf1.Ha[line1 - 1] == env.Xdf2.Ha[line2 - 1];

    private sealed class PatienceMap(XdfEnv env)
    {
        private readonly Dictionary<long, PatienceEntry> _byClassIdx = [];

        private readonly List<PatienceEntry> _inOrder = [];

        public XdfEnv Env { get; } = env;

        public bool HasMatches { get; private set; }

        public IReadOnlyList<PatienceEntry> Entries => _inOrder;

        public int Count => _inOrder.Count;

        public void InsertRecord(int line, bool pass2, long classIdx)
        {
            if (!_byClassIdx.TryGetValue(classIdx, out PatienceEntry? entry))
            {
                if (pass2)
                {
                    return;
                }

                entry = new PatienceEntry
                {
                    Line1 = line,
                    Line2 = 0,
                };
                _byClassIdx[classIdx] = entry;
                _inOrder.Add(entry);
                return;
            }

            if (pass2)
            {
                HasMatches = true;
            }

            if (!pass2 || entry.Line2 != 0)
            {
                entry.Line2 = NonUnique;
            }
            else
            {
                entry.Line2 = line;
            }
        }
    }

    private sealed class PatienceEntry
    {
        public int Line1 { get; set; }

        public int Line2 { get; set; }

        public PatienceEntry? Previous { get; set; }

        public PatienceEntry? Next { get; set; }
    }
}
