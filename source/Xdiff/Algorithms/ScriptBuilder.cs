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

internal static class ScriptBuilder
{
    internal static XdChange? BuildScript(XdfEnv env)
    {
        bool[] rchg1 = env.Xdf1.Rchg;
        bool[] rchg2 = env.Xdf2.Rchg;
        XdChange? cscr = null;

        int i1 = env.Xdf1.Nrec;
        int i2 = env.Xdf2.Nrec;
        while (i1 > 0 || i2 > 0)
        {
            if ((i1 > 0 && rchg1[i1]) || (i2 > 0 && rchg2[i2]))
            {
                int l1 = i1;
                while (i1 > 0 && rchg1[i1])
                {
                    i1--;
                }

                int l2 = i2;
                while (i2 > 0 && rchg2[i2])
                {
                    i2--;
                }

                cscr = AddChange(cscr, i1, i2, l1 - i1, l2 - i2);
            }

            i1--;
            i2--;
        }

        return cscr;
    }

    private static XdChange AddChange(XdChange? head, int i1, int i2, int chg1, int chg2)
    {
        return new XdChange
        {
            Next = head,
            I1 = i1,
            I2 = i2,
            Chg1 = chg1,
            Chg2 = chg2
        };
    }
}
