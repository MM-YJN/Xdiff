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

using System.Diagnostics;
using System.Globalization;

using Xdiff.Core;
using Xdiff.Util;

namespace Xdiff.Emit;

internal static class UnifiedDiffEmitter
{
    private const int MaxFuncLen = 80;

    internal static void Emit(XdfEnv env, XdChange? script, IHunkSink sink, DiffOptions options)
    {
        int ctxlen = options.ContextLines;
        int interhunk = options.InterHunkLines;
        bool funcCtx = options.FunctionContext;
        bool funcNames = options.IncludeFunctionNames;
        XdFile xdf1 = env.Xdf1;
        XdFile xdf2 = env.Xdf2;
        int funclineprev = -1;
        ReadOnlyMemory<byte> currentFunc = ReadOnlyMemory<byte>.Empty;

        XdChange? xch = script;

        while (xch is not null)
        {
            XdChange? xchp = xch;
            XdChange? xche = HunkGrouper.GetHunk(ref xch, ctxlen, interhunk);
            if (xch is null)
            {
                break;
            }

            Debug.Assert(xche is not null, "GetHunk returns a non-null hunk end when xch is non-null");

            int s1;
            int s2;
            int e1;
            int e2;

            while (true)
            {
                s1 = Math.Max(xch.I1 - ctxlen, 0);
                s2 = Math.Max(xch.I2 - ctxlen, 0);

                if (!funcCtx)
                {
                    break;
                }

                int i1 = xch.I1;
                if (i1 >= xdf1.Nrec)
                {
                    int i2 = xch.I2;
                    bool appendedWholeFunc = false;
                    while (i2 < xdf2.Nrec)
                    {
                        if (IsFuncRec(xdf2, options, i2))
                        {
                            appendedWholeFunc = true;
                            break;
                        }

                        i2++;
                    }

                    if (appendedWholeFunc)
                    {
                        break;
                    }

                    i1 = xdf1.Nrec - 1;
                }

                int fs1 = GetFuncLine(xdf1, options, i1, -1);
                while (fs1 > 0 && !IsEmptyRec(xdf1, fs1 - 1) && !IsFuncRec(xdf1, options, fs1 - 1))
                {
                    fs1--;
                }

                if (fs1 < 0)
                {
                    fs1 = 0;
                }

                if (fs1 < s1)
                {
                    s2 = Math.Max(s2 - (s1 - fs1), 0);
                    s1 = fs1;

                    while (!ReferenceEquals(xchp, xch))
                    {
                        Debug.Assert(xchp is not null, "xchp walks the change list back to xch");
                        if (xchp.I1 + xchp.Chg1 > s1 || xchp.I2 + xchp.Chg2 > s2)
                        {
                            break;
                        }

                        xchp = xchp.Next;
                    }

                    if (!ReferenceEquals(xchp, xch))
                    {
                        xch = xchp;
                        continue;
                    }
                }

                break;
            }

            while (true)
            {
                int lctx = ctxlen;
                lctx = Math.Min(lctx, xdf1.Nrec - (xche.I1 + xche.Chg1));
                lctx = Math.Min(lctx, xdf2.Nrec - (xche.I2 + xche.Chg2));
                e1 = xche.I1 + xche.Chg1 + lctx;
                e2 = xche.I2 + xche.Chg2 + lctx;

                if (!funcCtx)
                {
                    break;
                }

                int fe1 = GetFuncLine(xdf1, options, xche.I1 + xche.Chg1, xdf1.Nrec);
                while (fe1 > 0 && IsEmptyRec(xdf1, fe1 - 1))
                {
                    fe1--;
                }

                if (fe1 < 0)
                {
                    fe1 = xdf1.Nrec;
                }

                if (fe1 > e1)
                {
                    e2 = Math.Min(e2 + (fe1 - e1), xdf2.Nrec);
                    e1 = fe1;
                }

                if (xche.Next is not null)
                {
                    int l = Math.Min(xche.Next.I1, xdf1.Nrec - 1);
                    if (l - ctxlen <= e1 ||
                        GetFuncLine(xdf1, options, l, e1) < 0)
                    {
                        xche = xche.Next;
                        continue;
                    }
                }

                break;
            }

            if (funcNames)
            {
                int fi = GetFuncLine(xdf1, options, s1 - 1, funclineprev);
                funclineprev = s1 - 1;
                if (fi >= 0)
                {
                    currentFunc = SliceFuncName(xdf1, fi, options);
                }
            }

            sink.HunkHeader(s1, e1 - s1, s2, e2 - s2, currentFunc);

            int preOld = xch.I1 - xch.I2;
            for (int n = s2; n < xch.I2; n++)
            {
                EmitLine(sink, DiffLineKind.Context, xdf2, n, n + preOld + 1, n + 1);
            }

            int cur1 = xch.I1;
            int cur2 = xch.I2;
            XdChange xc = xch;
            while (true)
            {
                while (cur1 < xc.I1 && cur2 < xc.I2)
                {
                    EmitLine(sink, DiffLineKind.Context, xdf2, cur2, cur1 + 1, cur2 + 1);
                    cur1++;
                    cur2++;
                }

                for (int i = xc.I1; i < xc.I1 + xc.Chg1; i++)
                {
                    EmitLine(sink, DiffLineKind.Deletion, xdf1, i, i + 1, 0);
                    cur1++;
                }

                for (int i = xc.I2; i < xc.I2 + xc.Chg2; i++)
                {
                    EmitLine(sink, DiffLineKind.Addition, xdf2, i, 0, i + 1);
                    cur2++;
                }

                if (ReferenceEquals(xc, xche))
                {
                    break;
                }

                XdChange? next = xc.Next;
                Debug.Assert(next is not null, "xc.Next is non-null while more changes remain in the hunk");
                xc = next;
            }

            int postOld = (xche.I1 + xche.Chg1) - (xche.I2 + xche.Chg2);
            for (int n = xche.I2 + xche.Chg2; n < e2; n++)
            {
                EmitLine(sink, DiffLineKind.Context, xdf2, n, n + postOld + 1, n + 1);
            }

            xch = xche.Next;
        }
    }

    internal static bool DefaultFunctionMatcher(ReadOnlySpan<byte> line)
    {
        if (line.Length == 0)
        {
            return false;
        }

        byte c = line[0];
        bool isAlpha = (uint)(c - 'A') <= ('Z' - 'A') || (uint)(c - 'a') <= ('z' - 'a');
        return isAlpha || c == '_' || c == '$';
    }

    private static void EmitLine(IHunkSink sink, DiffLineKind kind, XdFile xdf, int recIndex, int oldLine, int newLine)
    {
        XdRecord rec = xdf.Recs[recIndex];
        sink.Line(kind, xdf.Data.Slice(rec.Offset, rec.Length), oldLine, newLine);
    }

    private static bool IsFuncRec(XdFile xdf, DiffOptions options, int ri)
    {
        XdRecord rec = xdf.Recs[ri];
        ReadOnlySpan<byte> span = xdf.Data.Span.Slice(rec.Offset, rec.Length);

        // The extractor (custom function-name path) takes precedence over
        // the plain bool matcher for the match decision.
        if (options.FunctionNameExtractor is { } extractor)
        {
            return extractor(span).IsMatch;
        }

        return options.FunctionMatcher is not null
            ? options.FunctionMatcher(span)
            : DefaultFunctionMatcher(span);
    }

    private static bool IsEmptyRec(XdFile xdf, int ri)
    {
        XdRecord rec = xdf.Recs[ri];
        ReadOnlySpan<byte> span = xdf.Data.Span.Slice(rec.Offset, rec.Length);
        return RecordMatch.IsBlankLine(span, WhitespaceMode.IgnoreAll);
    }

    private static int GetFuncLine(XdFile xdf, DiffOptions options, int start, int limit)
    {
        int step = start > limit ? -1 : 1;
        for (int l = start; l != limit && l >= 0 && l < xdf.Nrec; l += step)
        {
            if (IsFuncRec(xdf, options, l))
            {
                return l;
            }
        }

        return -1;
    }

    private static ReadOnlyMemory<byte> SliceFuncName(XdFile xdf, int ri, DiffOptions? options)
    {
        XdRecord rec = xdf.Recs[ri];
        ReadOnlyMemory<byte> data = xdf.Data;
        ReadOnlySpan<byte> span = data.Span.Slice(rec.Offset, rec.Length);

        // When a name extractor is configured (custom function-name path),
        // use its returned name slice directly. The extractor handles capture-
        // group extraction and trailing-whitespace trimming itself.
        if (options?.FunctionNameExtractor is { } extractor)
        {
            (bool extracted, Range nameRange) = extractor(span);
            if (extracted)
            {
                data = data.Slice(rec.Offset, rec.Length);
                return data[nameRange];
            }
        }

        // Default xdiff behavior: use the whole line, trimmed and capped.
        int len = rec.Length;
        if (len > MaxFuncLen)
        {
            len = MaxFuncLen;
        }

        ReadOnlySpan<byte> trimmed = data.Span.Slice(rec.Offset, len);
        while (len > 0 && Chars.IsSpace(trimmed[len - 1]))
        {
            len--;
        }

        return data.Slice(rec.Offset, len);
    }

    internal static int FormatNum(Span<char> buffer, long val)
    {
        return val.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture)
            ? written
            : 0;
    }
}
