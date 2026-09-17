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

using System.Buffers;

using Xdiff.Core;
using Xdiff.Util;

namespace Xdiff.Prepare;

internal static class FilePreparer
{
    private const int KpdisRun = 4;

    private const int MaxEqLimit = 1024;

    private const int SimScanWindow = 100;

    private const int GuessNlines1 = 256;

    private const int GuessNlines2 = 20;

    public static void PrepareEnv(
        ReadOnlyMemory<byte> file1,
        ReadOnlyMemory<byte> file2,
        WhitespaceMode ws,
        DiffAlgorithm alg,
        XdfEnv env)
    {
        env.Whitespace = ws;

        int sample = alg == DiffAlgorithm.Histogram ? GuessNlines2 : GuessNlines1;

        int enl1 = GuessLines(file1.Span, sample) + 1;
        int enl2 = GuessLines(file2.Span, sample) + 1;

        var cf = new XdClassifier(ws, enl1 + enl2 + 1);

        PrepareCtx(1, file1, enl1, ws, cf, env.Xdf1);
        PrepareCtx(2, file2, enl2, ws, cf, env.Xdf2);

        if (alg is DiffAlgorithm.Myers or DiffAlgorithm.Minimal)
        {
            TrimEnds(env.Xdf1, env.Xdf2);
            CleanupRecords(cf, env.Xdf1, env.Xdf2);
        }
    }

    private static int GuessLines(ReadOnlySpan<byte> file, int sample)
    {
        int nl = 0;
        int cur = 0;

        while (nl < sample && cur < file.Length)
        {
            nl++;
            int next = file[cur..].IndexOf((byte)'\n');
            if (next < 0)
            {
                cur = file.Length;
            }
            else
            {
                cur += next + 1;
            }
        }

        int tsize = cur;

        if (nl > 0 && tsize > 0)
        {
            nl = file.Length / (tsize / nl);
        }

        return nl + 1;
    }

    private static void PrepareCtx(
        int pass,
        ReadOnlyMemory<byte> file,
        int estNrec,
        WhitespaceMode ws,
        XdClassifier cf,
        XdFile xdf)
    {
        ReadOnlySpan<byte> span = file.Span;
        var recs = new List<XdRecord>(estNrec);
        var ha = new List<long>(estNrec);

        int offset = 0;
        int top = span.Length;

        while (offset < top)
        {
            int prev = offset;
            ulong rawHash = Hashing.HashRecord(span, ref offset, top, ws);
            int length = offset - prev;
            XdClass cls = cf.ClassifyRecord(pass, file.Slice(prev, length), rawHash);
            recs.Add(new XdRecord(prev, length, rawHash));
            ha.Add(cls.Idx);
        }

        int nrec = recs.Count;

        xdf.Nrec = nrec;
        xdf.Data = file;
        xdf.Recs = [.. recs];
        xdf.Rchg = new bool[nrec + 2];
        xdf.Ha = [.. ha];
        xdf.Nreff = 0;
        xdf.Dstart = 0;
        xdf.Dend = nrec - 1;
    }

    private static void TrimEnds(XdFile xdf1, XdFile xdf2)
    {
        int lim = Math.Min(xdf1.Nrec, xdf2.Nrec);

        int dstart = 0;
        while (dstart < lim && xdf1.Ha[dstart] == xdf2.Ha[dstart])
        {
            dstart++;
        }

        xdf1.Dstart = dstart;
        xdf2.Dstart = dstart;

        lim -= dstart;
        int matched = 0;
        while (matched < lim &&
               xdf1.Ha[xdf1.Nrec - 1 - matched] == xdf2.Ha[xdf2.Nrec - 1 - matched])
        {
            matched++;
        }

        xdf1.Dend = xdf1.Nrec - matched - 1;
        xdf2.Dend = xdf2.Nrec - matched - 1;
    }

    private static void CleanupRecords(XdClassifier cf, XdFile xdf1, XdFile xdf2)
    {
        // Rented: the fill loops write every slot in [Dstart, Dend] and
        // CleanMMatch clamps its scan window to exactly that range, so every
        // read slot was written in this same call — clearArray: false is safe.
        int[] dis1 = ArrayPool<int>.Shared.Rent(xdf1.Nrec + 1);
        int[] dis2 = ArrayPool<int>.Shared.Rent(xdf2.Nrec + 1);

        try
        {
            int mlim1 = (int)Math.Min(Numeric.BogoSqrt(xdf1.Nrec), MaxEqLimit);
            for (int i = xdf1.Dstart; i <= xdf1.Dend; i++)
            {
                XdClass cls = cf.GetClass((int)xdf1.Ha[i]);
                int nm = cls.Len2;
                dis1[i] = nm == 0 ? 0 : nm >= mlim1 ? 2 : 1;
            }

            int mlim2 = (int)Math.Min(Numeric.BogoSqrt(xdf2.Nrec), MaxEqLimit);
            for (int i = xdf2.Dstart; i <= xdf2.Dend; i++)
            {
                XdClass cls = cf.GetClass((int)xdf2.Ha[i]);
                int nm = cls.Len1;
                dis2[i] = nm == 0 ? 0 : nm >= mlim2 ? 2 : 1;
            }

            xdf1.Rindex = new int[xdf1.Nrec + 1];
            int nreff1 = 0;
            for (int i = xdf1.Dstart; i <= xdf1.Dend; i++)
            {
                if (dis1[i] == 1 ||
                    (dis1[i] == 2 && !CleanMMatch(dis1, i, xdf1.Dstart, xdf1.Dend)))
                {
                    xdf1.Rindex[nreff1] = i;
                    nreff1++;
                }
                else
                {
                    xdf1.Rchg[i + 1] = true;
                }
            }

            xdf1.Nreff = nreff1;

            xdf2.Rindex = new int[xdf2.Nrec + 1];
            int nreff2 = 0;
            for (int i = xdf2.Dstart; i <= xdf2.Dend; i++)
            {
                if (dis2[i] == 1 ||
                    (dis2[i] == 2 && !CleanMMatch(dis2, i, xdf2.Dstart, xdf2.Dend)))
                {
                    xdf2.Rindex[nreff2] = i;
                    nreff2++;
                }
                else
                {
                    xdf2.Rchg[i + 1] = true;
                }
            }

            xdf2.Nreff = nreff2;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(dis1, clearArray: false);
            ArrayPool<int>.Shared.Return(dis2, clearArray: false);
        }
    }

    private static bool CleanMMatch(int[] dis, int i, int s, int e)
    {
        if (i - s > SimScanWindow)
        {
            s = i - SimScanWindow;
        }

        if (e - i > SimScanWindow)
        {
            e = i + SimScanWindow;
        }

        int rdis0 = 0;
        int rpdis0 = 1;
        for (int r = 1; i - r >= s; r++)
        {
            if (dis[i - r] == 0)
            {
                rdis0++;
            }
            else if (dis[i - r] == 2)
            {
                rpdis0++;
            }
            else
            {
                break;
            }
        }

        if (rdis0 == 0)
        {
            return false;
        }

        int rdis1 = 0;
        int rpdis1 = 1;
        for (int r = 1; i + r <= e; r++)
        {
            if (dis[i + r] == 0)
            {
                rdis1++;
            }
            else if (dis[i + r] == 2)
            {
                rpdis1++;
            }
            else
            {
                break;
            }
        }

        if (rdis1 == 0)
        {
            return false;
        }

        rdis1 += rdis0;
        rpdis1 += rpdis0;

        return rpdis1 * KpdisRun < rpdis1 + rdis1;
    }
}
