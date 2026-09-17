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

namespace Xdiff.Util;

internal static class RecordMatch
{
    public static bool IsBlankLine(ReadOnlySpan<byte> line, WhitespaceMode flags)
    {
        if (flags == WhitespaceMode.None)
        {
            return line.Length <= 1;
        }

        int i = 0;
        while (i < line.Length && Chars.IsSpace(line[i]))
        {
            i++;
        }

        return i == line.Length;
    }

    public static bool RecordsEqual(ReadOnlySpan<byte> l1, ReadOnlySpan<byte> l2, WhitespaceMode flags)
    {
        if (l1.SequenceEqual(l2))
        {
            return true;
        }

        if (flags == WhitespaceMode.None)
        {
            return false;
        }

        int i1 = 0;
        int i2 = 0;

        if ((flags & WhitespaceMode.IgnoreAll) != 0)
        {
            SkipSpaces(l1, ref i1);
            SkipSpaces(l2, ref i2);
            while (i1 < l1.Length && i2 < l2.Length)
            {
                if (l1[i1++] != l2[i2++])
                {
                    return false;
                }

                SkipSpaces(l1, ref i1);
                SkipSpaces(l2, ref i2);
            }
        }
        else if ((flags & WhitespaceMode.IgnoreChanges) != 0)
        {
            while (i1 < l1.Length && i2 < l2.Length)
            {
                if (Chars.IsSpace(l1[i1]) && Chars.IsSpace(l2[i2]))
                {
                    SkipSpaces(l1, ref i1);
                    SkipSpaces(l2, ref i2);
                    continue;
                }

                if (l1[i1++] != l2[i2++])
                {
                    return false;
                }
            }
        }
        else if ((flags & WhitespaceMode.IgnoreAtEol) != 0)
        {
            while (i1 < l1.Length && i2 < l2.Length && l1[i1] == l2[i2])
            {
                i1++;
                i2++;
            }
        }
        else if ((flags & WhitespaceMode.IgnoreCrAtEol) != 0)
        {
            while (i1 < l1.Length && i2 < l2.Length && l1[i1] == l2[i2])
            {
                i1++;
                i2++;
            }

            return EndsWithOptionalCr(l1, l1.Length, i1) &&
                   EndsWithOptionalCr(l2, l2.Length, i2);
        }

        if (i1 < l1.Length)
        {
            SkipSpaces(l1, ref i1);
            if (l1.Length != i1)
            {
                return false;
            }
        }

        if (i2 < l2.Length)
        {
            SkipSpaces(l2, ref i2);
            return l2.Length == i2;
        }

        return true;
    }

    private static void SkipSpaces(ReadOnlySpan<byte> line, ref int i)
    {
        while (i < line.Length && Chars.IsSpace(line[i]))
        {
            i++;
        }
    }

    private static bool EndsWithOptionalCr(ReadOnlySpan<byte> l, int s, int i)
    {
        bool complete = s > 0 && l[s - 1] == '\n';
        if (complete)
        {
            s--;
        }

        if (s == i)
        {
            return true;
        }

        if (complete && s == i + 1 && l[i] == '\r')
        {
            return true;
        }

        return false;
    }
}
