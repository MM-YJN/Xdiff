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

internal static class Hashing
{
    public const ulong Seed = 5381;

    public static ulong HashRecord(ReadOnlySpan<byte> buffer, ref int offset, int top, WhitespaceMode flags)
    {
        if (flags != WhitespaceMode.None)
        {
            return HashRecordWithWhitespace(buffer, ref offset, top, flags);
        }

        ulong ha = Seed;
        int ptr = offset;
        while (ptr < top && buffer[ptr] != '\n')
        {
            ha += ha << 5;
            ha ^= buffer[ptr];
            ptr++;
        }

        offset = ptr < top ? ptr + 1 : ptr;
        return ha;
    }

    private static ulong HashRecordWithWhitespace(ReadOnlySpan<byte> buffer, ref int offset, int top, WhitespaceMode flags)
    {
        ulong ha = Seed;
        int ptr = offset;
        bool crAtEolOnly = flags == WhitespaceMode.IgnoreCrAtEol;

        while (ptr < top && buffer[ptr] != '\n')
        {
            byte c = buffer[ptr];
            if (crAtEolOnly)
            {
                if (c == '\r' && ptr + 1 < top && buffer[ptr + 1] == '\n')
                {
                    ptr++;
                    continue;
                }
            }
            else if (Chars.IsSpace(c))
            {
                int ptr2 = ptr;
                while (ptr + 1 < top && Chars.IsSpace(buffer[ptr + 1]) && buffer[ptr + 1] != '\n')
                {
                    ptr++;
                }

                bool atEol = top <= ptr + 1 || buffer[ptr + 1] == '\n';
                if ((flags & WhitespaceMode.IgnoreAll) == 0)
                {
                    if ((flags & WhitespaceMode.IgnoreChanges) != 0 && !atEol)
                    {
                        ha += ha << 5;
                        ha ^= (ulong)' ';
                    }
                    else if ((flags & WhitespaceMode.IgnoreAtEol) != 0 && !atEol)
                    {
                        while (ptr2 != ptr + 1)
                        {
                            ha += ha << 5;
                            ha ^= buffer[ptr2];
                            ptr2++;
                        }
                    }
                }

                ptr++;
                continue;
            }

            ha += ha << 5;
            ha ^= c;
            ptr++;
        }

        offset = ptr < top ? ptr + 1 : ptr;
        return ha;
    }
}
