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
using System.Diagnostics;

using Xdiff.Core;
using Xdiff.Util;

namespace Xdiff.Algorithms;

internal static class MyersDiff
{
    private const long MaxCostMin = 256;

    private const long HeurMinCost = 256;

    private const long LineMax = long.MaxValue;

    private const long SnakeCnt = 20;

    private const long KHeur = 4;

    internal static XdChange? Run(XdfEnv env, DiffAlgorithm alg, bool indentHeuristic)
    {
        switch (alg)
        {
            case DiffAlgorithm.Patience:
                PatienceDiff.Diff(env);
                break;
            case DiffAlgorithm.Histogram:
                HistogramDiff.Diff(env);
                break;
            default:
                DoDiff(env, alg);
                break;
        }

        ChangeCompact.Compact(env.Xdf1, env.Xdf2, indentHeuristic);
        ChangeCompact.Compact(env.Xdf2, env.Xdf1, indentHeuristic);
        return ScriptBuilder.BuildScript(env);
    }

    internal static void DoDiff(XdfEnv env, DiffAlgorithm alg)
    {
        XdFile xdf1 = env.Xdf1;
        XdFile xdf2 = env.Xdf2;
        int ndiags = xdf1.Nreff + xdf2.Nreff + 3;

        // Rented from the pool: the arrays are shared across the recursive
        // RecsCompare/Split calls (each Split re-seeds its own region and the
        // parent never reads the arrays after a child returns), and every
        // index is bounded by the computed kvd size, so oversize rentals are
        // harmless and clearArray: false is safe.
        long[] kvdf = ArrayPool<long>.Shared.Rent(2 * ndiags + 2);
        long[] kvdb = ArrayPool<long>.Shared.Rent(2 * ndiags + 2);
        int baseOffset = xdf2.Nreff + 1;

        long mxcost = Numeric.BogoSqrt(ndiags);
        if (mxcost < MaxCostMin)
        {
            mxcost = MaxCostMin;
        }

        var xenv = new XdAlgoEnv(mxcost, SnakeCnt, HeurMinCost);

        try
        {
            DiffData dd1 = BuildDiffData(xdf1);
            DiffData dd2 = BuildDiffData(xdf2);

            bool needMin = alg == DiffAlgorithm.Minimal;
            RecsCompare(dd1, 0, dd1.Nrec, dd2, 0, dd2.Nrec,
                kvdf, kvdb, baseOffset, baseOffset, needMin, xenv);
        }
        finally
        {
            ArrayPool<long>.Shared.Return(kvdf, clearArray: false);
            ArrayPool<long>.Shared.Return(kvdb, clearArray: false);
        }
    }

    private static DiffData BuildDiffData(XdFile xdf)
    {
        int[]? rindex = xdf.Rindex;
        Debug.Assert(rindex is not null, "Rindex is populated by FilePreparer before Myers runs.");
        long[] compactedHa = new long[xdf.Nreff];
        for (int j = 0; j < xdf.Nreff; j++)
        {
            compactedHa[j] = xdf.Ha[rindex[j]];
        }

        return new DiffData(xdf.Nreff, compactedHa, rindex, xdf.Rchg);
    }

    private static void RecsCompare(
        DiffData dd1, int off1, int lim1,
        DiffData dd2, int off2, int lim2,
        long[] kvdf, long[] kvdb, int kvdfBase, int kvdbBase,
        bool needMin, XdAlgoEnv env)
    {
        long[] ha1 = dd1.Ha;
        long[] ha2 = dd2.Ha;

        for (; off1 < lim1 && off2 < lim2 && ha1[off1] == ha2[off2]; off1++, off2++)
        {
        }

        for (; off1 < lim1 && off2 < lim2 && ha1[lim1 - 1] == ha2[lim2 - 1]; lim1--, lim2--)
        {
        }

        if (off1 == lim1)
        {
            for (; off2 < lim2; off2++)
            {
                dd2.Rchg[dd2.Rindex[off2] + 1] = true;
            }
        }
        else if (off2 == lim2)
        {
            for (; off1 < lim1; off1++)
            {
                dd1.Rchg[dd1.Rindex[off1] + 1] = true;
            }
        }
        else
        {
            var spl = new XdSplit(0, 0, false, false);
            Split(ha1, off1, lim1, ha2, off2, lim2,
                kvdf, kvdb, kvdfBase, kvdbBase, needMin, ref spl, env);
            RecsCompare(dd1, off1, spl.I1, dd2, off2, spl.I2,
                kvdf, kvdb, kvdfBase, kvdbBase, spl.MinLo, env);
            RecsCompare(dd1, spl.I1, lim1, dd2, spl.I2, lim2,
                kvdf, kvdb, kvdfBase, kvdbBase, spl.MinHi, env);
        }
    }

    private static long Split(
        long[] ha1, int off1, int lim1,
        long[] ha2, int off2, int lim2,
        long[] kvdf, long[] kvdb, int kvdfBase, int kvdbBase,
        bool needMin, ref XdSplit spl, XdAlgoEnv xenv)
    {
        int dmin = off1 - lim2;
        int dmax = lim1 - off2;
        int fmid = off1 - off2;
        int bmid = lim1 - lim2;
        int odd = (fmid - bmid) & 1;
        int fmin = fmid;
        int fmax = fmid;
        int bmin = bmid;
        int bmax = bmid;

        kvdf[kvdfBase + fmid] = off1;
        kvdb[kvdbBase + bmid] = lim1;

        for (long ec = 1L; ; ec++)
        {
            bool gotSnake = false;

            if (fmin > dmin)
            {
                kvdf[kvdfBase + --fmin - 1] = -1;
            }
            else
            {
                ++fmin;
            }

            if (fmax < dmax)
            {
                kvdf[kvdfBase + ++fmax + 1] = -1;
            }
            else
            {
                --fmax;
            }

            for (int d = fmax; d >= fmin; d -= 2)
            {
                int i1;
                if (kvdf[kvdfBase + d - 1] >= kvdf[kvdfBase + d + 1])
                {
                    i1 = (int)(kvdf[kvdfBase + d - 1] + 1);
                }
                else
                {
                    i1 = (int)kvdf[kvdfBase + d + 1];
                }

                int prev1 = i1;
                int i2 = i1 - d;
                for (; i1 < lim1 && i2 < lim2 && ha1[i1] == ha2[i2]; i1++, i2++)
                {
                }

                if (i1 - prev1 > xenv.SnakeCnt)
                {
                    gotSnake = true;
                }

                kvdf[kvdfBase + d] = i1;
                if (odd != 0 && bmin <= d && d <= bmax && kvdb[kvdbBase + d] <= i1)
                {
                    spl = new XdSplit(i1, i2, true, true);
                    return ec;
                }
            }

            if (bmin > dmin)
            {
                kvdb[kvdbBase + --bmin - 1] = LineMax;
            }
            else
            {
                ++bmin;
            }

            if (bmax < dmax)
            {
                kvdb[kvdbBase + ++bmax + 1] = LineMax;
            }
            else
            {
                --bmax;
            }

            for (int d = bmax; d >= bmin; d -= 2)
            {
                int i1;
                if (kvdb[kvdbBase + d - 1] < kvdb[kvdbBase + d + 1])
                {
                    i1 = (int)kvdb[kvdbBase + d - 1];
                }
                else
                {
                    i1 = (int)(kvdb[kvdbBase + d + 1] - 1);
                }

                int prev1 = i1;
                int i2 = i1 - d;
                for (; i1 > off1 && i2 > off2 && ha1[i1 - 1] == ha2[i2 - 1]; i1--, i2--)
                {
                }

                if (prev1 - i1 > xenv.SnakeCnt)
                {
                    gotSnake = true;
                }

                kvdb[kvdbBase + d] = i1;
                if (odd == 0 && fmin <= d && d <= fmax && i1 <= kvdf[kvdfBase + d])
                {
                    spl = new XdSplit(i1, i2, true, true);
                    return ec;
                }
            }

            if (needMin)
            {
                continue;
            }

            if (gotSnake && ec > xenv.HeurMinCost)
            {
                long best = 0L;

                for (int d = fmax; d >= fmin; d -= 2)
                {
                    int dd = d > fmid ? d - fmid : fmid - d;
                    int i1 = (int)kvdf[kvdfBase + d];
                    int i2 = i1 - d;
                    int v = (i1 - off1) + (i2 - off2) - dd;

                    if (v > KHeur * ec && v > best &&
                        off1 + (int)xenv.SnakeCnt <= i1 && i1 < lim1 &&
                        off2 + (int)xenv.SnakeCnt <= i2 && i2 < lim2)
                    {
                        for (int k = 1; ha1[i1 - k] == ha2[i2 - k]; k++)
                        {
                            if (k == xenv.SnakeCnt)
                            {
                                best = v;
                                spl = new XdSplit(i1, i2, true, false);
                                break;
                            }
                        }
                    }
                }

                if (best > 0)
                {
                    return ec;
                }

                for (int d = bmax; d >= bmin; d -= 2)
                {
                    int dd = d > bmid ? d - bmid : bmid - d;
                    int i1 = (int)kvdb[kvdbBase + d];
                    int i2 = i1 - d;
                    int v = (lim1 - i1) + (lim2 - i2) - dd;

                    if (v > KHeur * ec && v > best &&
                        off1 < i1 && i1 <= lim1 - (int)xenv.SnakeCnt &&
                        off2 < i2 && i2 <= lim2 - (int)xenv.SnakeCnt)
                    {
                        for (int k = 0; ha1[i1 + k] == ha2[i2 + k]; k++)
                        {
                            if (k == xenv.SnakeCnt - 1)
                            {
                                best = v;
                                spl = new XdSplit(i1, i2, false, true);
                                break;
                            }
                        }
                    }
                }

                if (best > 0)
                {
                    return ec;
                }
            }

            if (ec >= xenv.Mxcost)
            {
                long fbest = -1L;
                long fbest1 = -1L;

                for (int d = fmax; d >= fmin; d -= 2)
                {
                    int i1 = Math.Min((int)kvdf[kvdfBase + d], lim1);
                    int i2 = i1 - d;
                    if (lim2 < i2)
                    {
                        i1 = lim2 + d;
                        i2 = lim2;
                    }

                    if (fbest < i1 + i2)
                    {
                        fbest = i1 + i2;
                        fbest1 = i1;
                    }
                }

                long bbest = LineMax;
                long bbest1 = LineMax;

                for (int d = bmax; d >= bmin; d -= 2)
                {
                    int i1 = Math.Max(off1, (int)kvdb[kvdbBase + d]);
                    int i2 = i1 - d;
                    if (i2 < off2)
                    {
                        i1 = off2 + d;
                        i2 = off2;
                    }

                    if (i1 + i2 < bbest)
                    {
                        bbest = i1 + i2;
                        bbest1 = i1;
                    }
                }

                if ((lim1 + lim2) - bbest < fbest - (off1 + off2))
                {
                    spl = new XdSplit((int)fbest1, (int)(fbest - fbest1), true, false);
                }
                else
                {
                    spl = new XdSplit((int)bbest1, (int)(bbest - bbest1), false, true);
                }

                return ec;
            }
        }
    }
}

