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

namespace Xdiff.Emit;

internal static class HunkGrouper
{
    internal static XdChange? GetHunk(ref XdChange? xscr, int ctxlen, int interhunkctxlen)
    {
        long maxCommon = 2L * ctxlen + interhunkctxlen;
        long maxIgnorable = (long)ctxlen;
        long ignored = 0;

        XdChange? xchp = xscr;
        while (xchp is not null && xchp.Ignore)
        {
            XdChange? next = xchp.Next;
            if (next is null || next.I1 - (xchp.I1 + xchp.Chg1) >= maxIgnorable)
            {
                xscr = next;
            }

            xchp = next;
        }

        if (xscr is null)
        {
            return null;
        }

        XdChange lxch = xscr;

        xchp = xscr;
        XdChange? xch = xchp.Next;
        while (xch is not null)
        {
            long distance = (long)xch.I1 - (xchp.I1 + xchp.Chg1);
            if (distance > maxCommon)
            {
                break;
            }

            if (distance < maxIgnorable && (!xch.Ignore || ReferenceEquals(lxch, xchp)))
            {
                lxch = xch;
                ignored = 0;
            }
            else if (distance < maxIgnorable && xch.Ignore)
            {
                ignored += xch.Chg2;
            }
            else if (!ReferenceEquals(lxch, xchp) &&
                     xch.I1 + ignored - (lxch.I1 + lxch.Chg1) > maxCommon)
            {
                break;
            }
            else if (!xch.Ignore)
            {
                lxch = xch;
                ignored = 0;
            }
            else
            {
                ignored += xch.Chg2;
            }

            xchp = xch;
            xch = xch.Next;
        }

        return lxch;
    }
}
