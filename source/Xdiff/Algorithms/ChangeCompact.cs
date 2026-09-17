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

using Xdiff.Core;

namespace Xdiff.Algorithms;

internal static class ChangeCompact
{
    internal static void Compact(XdFile xdf, XdFile xdfo, bool useIndentHeuristic)
    {
        var g = new XdGroup();
        var go = new XdGroup();
        GroupInit(xdf, ref g);
        GroupInit(xdfo, ref go);

        while (true)
        {
            if (g.End != g.Start)
            {
                int groupsize;
                int endMatchingOther;
                int earliestEnd;

                do
                {
                    groupsize = g.End - g.Start;
                    endMatchingOther = -1;

                    while (GroupSlideUp(xdf, ref g))
                    {
                        if (!GroupPrevious(xdfo, ref go))
                        {
                            throw new InvalidOperationException("group sync broken sliding up");
                        }
                    }

                    earliestEnd = g.End;

                    if (go.End > go.Start)
                    {
                        endMatchingOther = g.End;
                    }

                    while (true)
                    {
                        if (!GroupSlideDown(xdf, ref g))
                        {
                            break;
                        }

                        if (!GroupNext(xdfo, ref go))
                        {
                            throw new InvalidOperationException("group sync broken sliding down");
                        }

                        if (go.End > go.Start)
                        {
                            endMatchingOther = g.End;
                        }
                    }
                } while (groupsize != g.End - g.Start);

                if (g.End == earliestEnd)
                {
                }
                else if (endMatchingOther != -1)
                {
                    while (go.End == go.Start)
                    {
                        if (!GroupSlideUp(xdf, ref g))
                        {
                            throw new InvalidOperationException("match disappeared");
                        }

                        if (!GroupPrevious(xdfo, ref go))
                        {
                            throw new InvalidOperationException("group sync broken sliding to match");
                        }
                    }
                }
                else if (useIndentHeuristic)
                {
                    IndentHeuristic.Apply(xdf, xdfo, ref g, ref go, groupsize, earliestEnd);
                }
            }

            if (!GroupNext(xdf, ref g))
            {
                break;
            }

            if (!GroupNext(xdfo, ref go))
            {
                throw new InvalidOperationException("group sync broken moving to next group");
            }
        }

        if (GroupNext(xdfo, ref go))
        {
            throw new InvalidOperationException("group sync broken at end of file");
        }
    }

    internal static void GroupInit(XdFile xdf, ref XdGroup g)
    {
        g.Start = 0;
        g.End = 0;
        while (xdf.Rchg[g.End + 1])
        {
            g.End++;
        }
    }

    internal static bool GroupNext(XdFile xdf, ref XdGroup g)
    {
        if (g.End == xdf.Nrec)
        {
            return false;
        }

        g.Start = g.End + 1;
        g.End = g.Start;
        while (xdf.Rchg[g.End + 1])
        {
            g.End++;
        }

        return true;
    }

    internal static bool GroupPrevious(XdFile xdf, ref XdGroup g)
    {
        if (g.Start == 0)
        {
            return false;
        }

        g.End = g.Start - 1;
        g.Start = g.End;
        while (xdf.Rchg[g.Start])
        {
            g.Start--;
        }

        return true;
    }

    private static bool GroupSlideDown(XdFile xdf, ref XdGroup g)
    {
        if (g.End < xdf.Nrec && xdf.Ha[g.Start] == xdf.Ha[g.End])
        {
            xdf.Rchg[g.Start + 1] = false;
            g.Start++;
            xdf.Rchg[g.End + 1] = true;
            g.End++;
            while (xdf.Rchg[g.End + 1])
            {
                g.End++;
            }

            return true;
        }

        return false;
    }

    internal static bool GroupSlideUp(XdFile xdf, ref XdGroup g)
    {
        if (g.Start > 0 && xdf.Ha[g.Start - 1] == xdf.Ha[g.End - 1])
        {
            g.Start--;
            xdf.Rchg[g.Start + 1] = true;
            g.End--;
            xdf.Rchg[g.End + 1] = false;
            while (xdf.Rchg[g.Start])
            {
                g.Start--;
            }

            return true;
        }

        return false;
    }
}
