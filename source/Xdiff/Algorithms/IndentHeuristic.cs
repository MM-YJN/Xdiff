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
using Xdiff.Util;

namespace Xdiff.Algorithms;

internal static class IndentHeuristic
{
    private const int MaxIndent = 200;

    private const int MaxBlanks = 20;

    private const int IndentHeuristicMaxSliding = 100;

    private const int StartOfFilePenalty = 1;

    private const int EndOfFilePenalty = 21;

    private const int TotalBlankWeight = -30;

    private const int PostBlankWeight = 6;

    private const int RelativeIndentPenalty = -4;

    private const int RelativeIndentWithBlankPenalty = 10;

    private const int RelativeOutdentPenalty = 24;

    private const int RelativeOutdentWithBlankPenalty = 17;

    private const int RelativeDedentPenalty = 23;

    private const int RelativeDedentWithBlankPenalty = 17;

    private const int IndentWeight = 60;

    internal static void Apply(
        XdFile xdf, XdFile xdfo, ref XdGroup g, ref XdGroup go, int groupsize, int earliestEnd)
    {
        int bestShift = -1;
        var bestScore = new SplitScore();

        int shift = earliestEnd;
        if (g.End - groupsize - 1 > shift)
        {
            shift = g.End - groupsize - 1;
        }

        if (g.End - IndentHeuristicMaxSliding > shift)
        {
            shift = g.End - IndentHeuristicMaxSliding;
        }

        for (; shift <= g.End; shift++)
        {
            var m = new SplitMeasurement();
            var score = new SplitScore();

            MeasureSplit(xdf, shift, ref m);
            ScoreAddSplit(ref m, ref score);
            MeasureSplit(xdf, shift - groupsize, ref m);
            ScoreAddSplit(ref m, ref score);

            if (bestShift == -1 || ScoreCmp(score, bestScore) <= 0)
            {
                bestScore = score;
                bestShift = shift;
            }
        }

        while (g.End > bestShift)
        {
            if (!ChangeCompact.GroupSlideUp(xdf, ref g))
            {
                throw new InvalidOperationException("best shift unreached");
            }

            if (!ChangeCompact.GroupPrevious(xdfo, ref go))
            {
                throw new InvalidOperationException("group sync broken sliding to blank line");
            }
        }
    }

    private static int GetIndent(XdFile xdf, int recIdx)
    {
        XdRecord rec = xdf.Recs[recIdx];
        ReadOnlySpan<byte> span = xdf.Data.Span.Slice(rec.Offset, rec.Length);
        int ret = 0;

        for (int i = 0; i < span.Length; i++)
        {
            byte c = span[i];
            if (!Chars.IsSpace(c))
            {
                return ret;
            }

            if (c == (byte)' ')
            {
                ret += 1;
            }
            else if (c == (byte)'\t')
            {
                ret += 8 - ret % 8;
            }

            if (ret >= MaxIndent)
            {
                return MaxIndent;
            }
        }

        return -1;
    }

    private static void MeasureSplit(XdFile xdf, int split, ref SplitMeasurement m)
    {
        if (split >= xdf.Nrec)
        {
            m.EndOfFile = 1;
            m.Indent = -1;
        }
        else
        {
            m.EndOfFile = 0;
            m.Indent = GetIndent(xdf, split);
        }

        m.PreBlank = 0;
        m.PreIndent = -1;
        for (int i = split - 1; i >= 0; i--)
        {
            m.PreIndent = GetIndent(xdf, i);
            if (m.PreIndent != -1)
            {
                break;
            }

            m.PreBlank += 1;
            if (m.PreBlank == MaxBlanks)
            {
                m.PreIndent = 0;
                break;
            }
        }

        m.PostBlank = 0;
        m.PostIndent = -1;
        for (int i = split + 1; i < xdf.Nrec; i++)
        {
            m.PostIndent = GetIndent(xdf, i);
            if (m.PostIndent != -1)
            {
                break;
            }

            m.PostBlank += 1;
            if (m.PostBlank == MaxBlanks)
            {
                m.PostIndent = 0;
                break;
            }
        }
    }

    private static void ScoreAddSplit(ref SplitMeasurement m, ref SplitScore s)
    {
        if (m.PreIndent == -1 && m.PreBlank == 0)
        {
            s.Penalty += StartOfFilePenalty;
        }

        if (m.EndOfFile != 0)
        {
            s.Penalty += EndOfFilePenalty;
        }

        int postBlank = m.Indent == -1 ? 1 + m.PostBlank : 0;
        int totalBlank = m.PreBlank + postBlank;

        s.Penalty += TotalBlankWeight * totalBlank;
        s.Penalty += PostBlankWeight * postBlank;

        int indent = m.Indent != -1 ? m.Indent : m.PostIndent;
        bool anyBlanks = totalBlank != 0;

        s.EffectiveIndent += indent;

        if (indent != -1 && m.PreIndent != -1)
        {
            if (indent > m.PreIndent)
            {
                s.Penalty += anyBlanks
                    ? RelativeIndentWithBlankPenalty
                    : RelativeIndentPenalty;
            }
            else if (indent < m.PreIndent)
            {
                if (m.PostIndent != -1 && m.PostIndent > indent)
                {
                    s.Penalty += anyBlanks
                        ? RelativeOutdentWithBlankPenalty
                        : RelativeOutdentPenalty;
                }
                else
                {
                    s.Penalty += anyBlanks
                        ? RelativeDedentWithBlankPenalty
                        : RelativeDedentPenalty;
                }
            }
        }
    }

    private static int ScoreCmp(SplitScore s1, SplitScore s2)
    {
        int cmpIndents = (s1.EffectiveIndent > s2.EffectiveIndent ? 1 : 0) -
                         (s1.EffectiveIndent < s2.EffectiveIndent ? 1 : 0);
        return IndentWeight * cmpIndents + (s1.Penalty - s2.Penalty);
    }

    private struct SplitMeasurement
    {
        public int EndOfFile { get; set; }

        public int Indent { get; set; }

        public int PreBlank { get; set; }

        public int PreIndent { get; set; }

        public int PostBlank { get; set; }

        public int PostIndent { get; set; }
    }

    private struct SplitScore
    {
        public int EffectiveIndent { get; set; }

        public int Penalty { get; set; }
    }
}
