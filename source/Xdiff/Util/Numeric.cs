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

using System.Globalization;

namespace Xdiff.Util;

internal static class Numeric
{
    public static long BogoSqrt(long n)
    {
        long i = 1L;
        while (n > 0)
        {
            n >>= 2;
            i <<= 1;
        }

        return i;
    }

    public static int HashBits(int size)
    {
        int val = 1;
        int bits = 0;
        while (val < size && bits < 32)
        {
            val <<= 1;
            bits++;
        }

        return bits != 0 ? bits : 1;
    }

    public static int NumOut(Span<char> buffer, long val)
    {
        return val.TryFormat(buffer, out int written, provider: CultureInfo.InvariantCulture)
            ? written
            : 0;
    }
}
