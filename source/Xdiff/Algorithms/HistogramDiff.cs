/*
 * Copyright (C) 2010, Google Inc.
 * and other copyright owners as documented in JGit's IP log.
 *
 * This program and the accompanying materials are made available
 * under the terms of the Eclipse Distribution License v1.0 which
 * accompanies this distribution, is reproduced below, and is
 * available at http://www.eclipse.org/org/documents/edl-v10.php
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the Eclipse Foundation, Inc. nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

/*
 *  LibXDiff by Davide Libenzi ( File Differential Library )
 *  Copyright (C) 2003  Davide Libenzi
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
// Local C# modifications: LGPL-2.1-or-later; upstream Histogram: EDL-1.0.
// SPDX-License-Identifier: LGPL-2.1-or-later AND BSD-3-Clause
// See LICENSE for license terms and the disclaimer of warranty.

using System.Buffers;

using Xdiff.Core;
using Xdiff.Util;

namespace Xdiff.Algorithms;

internal static class HistogramDiff
{
    private const int MaxPtr = int.MaxValue;

    private const int MaxCnt = int.MaxValue;

    private const int ChainLimit = 64;

    internal static void Diff(XdfEnv env)
    {
        int line1 = env.Xdf1.Dstart + 1;
        int count1 = env.Xdf1.Dend - env.Xdf1.Dstart + 1;
        int line2 = env.Xdf2.Dstart + 1;
        int count2 = env.Xdf2.Dend - env.Xdf2.Dstart + 1;
        HistogramDiffLoop(env, line1, count1, line2, count2);
    }

    private static void HistogramDiffLoop(
        XdfEnv env, int line1, int count1, int line2, int count2)
    {
        while (true)
        {
            if (count1 <= 0 && count2 <= 0)
            {
                return;
            }

            if (line1 + count1 - 1 >= MaxPtr)
            {
                // The reference implementation returns an error (-1) here and
                // aborts the whole diff; this condition is unreachable with
                // sequential class indices, but a silent partial result would
                // be worse than failing loudly if it ever fires.
                throw new InvalidOperationException("histogram diff: line region exceeds the supported maximum");
            }

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

            var lcs = new Region();
            int lcsFound = FindLcs(env, ref lcs, line1, count1, line2, count2);

            if (lcsFound < 0)
            {
                // Mirrors the reference implementation's error propagation:
                // xdl_do_histogram_diff() returns -1 and xdl_diff() aborts with
                // no output. Unreachable with sequential class indices (bucket
                // chains stay <= 2), but never silently emit a partial diff.
                throw new InvalidOperationException("histogram diff: search table chain overflow");
            }

            if (lcsFound > 0)
            {
                FallBackDiff.Diff(env, line1, count1, line2, count2);
                return;
            }

            if (lcs.Begin1 == 0 && lcs.Begin2 == 0)
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

            HistogramDiffLoop(env, line1, lcs.Begin1 - line1, line2, lcs.Begin2 - line2);

            count1 = line1 + count1 - 1 - lcs.End1;
            line1 = lcs.End1 + 1;
            count2 = line2 + count2 - 1 - lcs.End2;
            line2 = lcs.End2 + 1;
        }
    }

    private static int FindLcs(
        XdfEnv env, ref Region lcs, int line1, int count1, int line2, int count2)
    {
        using var index = new HistIndex(env, line1, count1);

        if (!ScanA(index, line1, count1))
        {
            return -1;
        }

        index.Cnt = index.MaxChainLength + 1;

        int bPtr = line2;
        int lineEnd2 = line2 + count2 - 1;
        while (bPtr <= lineEnd2)
        {
            bPtr = TryLcs(index, ref lcs, bPtr, line1, count1, line2, count2);
        }

        if (index.HasCommon && index.MaxChainLength < index.Cnt)
        {
            return 1;
        }

        return 0;
    }

    private static bool ScanA(HistIndex index, int line1, int count1)
    {
        long[] ha1 = index.Env.Xdf1.Ha;
        int lineEnd1 = line1 + count1 - 1;

        for (int ptr = lineEnd1; line1 <= ptr; ptr--)
        {
            int tblIdx = HashLong(ha1[ptr - 1], index.TableBits);
            int recIdx = index.Records[tblIdx];

            int chainLen = 0;
            bool found = false;

            while (recIdx >= 0)
            {
                HistRecord rec = index.Pool[recIdx];
                if (ha1[rec.Ptr - 1] == ha1[ptr - 1])
                {
                    index.NextPtrs[ptr - index.PtrShift] = rec.Ptr;
                    rec.Ptr = ptr;
                    rec.Cnt = Math.Min(MaxCnt, rec.Cnt + 1);
                    index.Pool[recIdx] = rec;
                    index.LineMap[ptr - index.PtrShift] = recIdx;
                    found = true;
                    break;
                }

                recIdx = rec.Next;
                chainLen++;
            }

            if (found)
            {
                continue;
            }

            if (chainLen == index.MaxChainLength)
            {
                return false;
            }

            var newRec = new HistRecord(ptr, 1, index.Records[tblIdx]);
            index.Pool.Add(newRec);
            int newIdx = index.Pool.Count - 1;
            index.Records[tblIdx] = newIdx;
            index.LineMap[ptr - index.PtrShift] = newIdx;
        }

        return true;
    }

    private static int TryLcs(
        HistIndex index, ref Region lcs, int bPtr,
        int line1, int count1, int line2, int count2)
    {
        int bNext = bPtr + 1;
        long[] ha1 = index.Env.Xdf1.Ha;
        long[] ha2 = index.Env.Xdf2.Ha;
        int lineEnd1 = line1 + count1 - 1;
        int lineEnd2 = line2 + count2 - 1;

        int recIdx = index.Records[HashLong(ha2[bPtr - 1], index.TableBits)];

        while (recIdx >= 0)
        {
            HistRecord rec = index.Pool[recIdx];

            if (rec.Cnt > index.Cnt)
            {
                if (!index.HasCommon)
                {
                    index.HasCommon = ha1[rec.Ptr - 1] == ha2[bPtr - 1];
                }

                recIdx = rec.Next;
                continue;
            }

            int asPtr = rec.Ptr;
            if (ha1[asPtr - 1] != ha2[bPtr - 1])
            {
                recIdx = rec.Next;
                continue;
            }

            index.HasCommon = true;

            while (true)
            {
                bool shouldBreak = false;
                int np = index.NextPtrs[asPtr - index.PtrShift];
                int bs = bPtr;
                int ae = asPtr;
                int be = bs;
                int rc = rec.Cnt;

                while (line1 < asPtr && line2 < bs &&
                       ha1[asPtr - 2] == ha2[bs - 2])
                {
                    asPtr--;
                    bs--;
                    if (rc > 1)
                    {
                        rc = Math.Min(rc, index.Pool[index.LineMap[asPtr - index.PtrShift]].Cnt);
                    }
                }

                while (ae < lineEnd1 && be < lineEnd2 &&
                       ha1[ae] == ha2[be])
                {
                    ae++;
                    be++;
                    if (rc > 1)
                    {
                        rc = Math.Min(rc, index.Pool[index.LineMap[ae - index.PtrShift]].Cnt);
                    }
                }

                if (bNext <= be)
                {
                    bNext = be + 1;
                }

                if (lcs.End1 - lcs.Begin1 < ae - asPtr || rc < index.Cnt)
                {
                    lcs.Begin1 = asPtr;
                    lcs.Begin2 = bs;
                    lcs.End1 = ae;
                    lcs.End2 = be;
                    index.Cnt = rc;
                }

                if (np == 0)
                {
                    break;
                }

                while (np <= ae)
                {
                    np = index.NextPtrs[np - index.PtrShift];
                    if (np == 0)
                    {
                        shouldBreak = true;
                        break;
                    }
                }

                if (shouldBreak)
                {
                    break;
                }

                asPtr = np;
            }

            recIdx = rec.Next;
        }

        return bNext;
    }

    private static int HashLong(long v, int bits) => (int)((v + (v >> bits)) & ((1L << bits) - 1));

    private struct Region
    {
        public int Begin1 { get; set; }

        public int End1 { get; set; }

        public int Begin2 { get; set; }

        public int End2 { get; set; }
    }

    private struct HistRecord(int ptr, int cnt, int next)
    {
        public int Ptr { get; set; } = ptr;

        public int Cnt { get; set; } = cnt;

        public int Next { get; set; } = next;
    }

    private sealed class HistIndex : IDisposable
    {
        public HistIndex(XdfEnv env, int line1, int count1)
        {
            Env = env;
            TableBits = Numeric.HashBits(count1);
            RecordsSize = 1 << TableBits;
            LineMapSize = count1;
            PtrShift = line1;

            // Rented scratch: Records/LineMap are fully overwritten with -1
            // before use (clearArray: false on return); NextPtrs must read 0
            // for the oldest occurrence of a line class, so it is cleared here
            // AND returned with clearArray: true so stale chain pointers can
            // never leak into a later diff.
            Records = ArrayPool<int>.Shared.Rent(RecordsSize);
            Array.Fill(Records, -1, 0, RecordsSize);
            LineMap = ArrayPool<int>.Shared.Rent(LineMapSize);
            Array.Fill(LineMap, -1, 0, LineMapSize);
            NextPtrs = ArrayPool<int>.Shared.Rent(LineMapSize);
            Array.Clear(NextPtrs, 0, LineMapSize);
        }

        public XdfEnv Env { get; }

        public int[] Records { get; }

        public int[] LineMap { get; }

        public int[] NextPtrs { get; }

        public List<HistRecord> Pool { get; } = [];

        public int TableBits { get; }

        public int RecordsSize { get; }

        public int LineMapSize { get; }

        public int MaxChainLength { get; } = ChainLimit;

        public int PtrShift { get; }

        public int Cnt { get; set; }

        public bool HasCommon { get; set; }

        public void Dispose()
        {
            ArrayPool<int>.Shared.Return(Records, clearArray: false);
            ArrayPool<int>.Shared.Return(LineMap, clearArray: false);
            ArrayPool<int>.Shared.Return(NextPtrs, clearArray: true);
        }
    }
}
