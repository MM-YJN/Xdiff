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
using Xdiff.Prepare;

namespace Xdiff.Algorithms;

internal static class FallBackDiff
{
    internal static void Diff(XdfEnv env, int line1, int count1, int line2, int count2)
    {
        WhitespaceMode ws = env.Whitespace;

        ReadOnlyMemory<byte> sub1 = ExtractSubBuffer(env.Xdf1, line1, count1);
        ReadOnlyMemory<byte> sub2 = ExtractSubBuffer(env.Xdf2, line2, count2);

        var subEnv = new XdfEnv { Whitespace = ws };
        FilePreparer.PrepareEnv(sub1, sub2, ws, DiffAlgorithm.Myers, subEnv);
        MyersDiff.DoDiff(subEnv, DiffAlgorithm.Myers);

        Array.Copy(subEnv.Xdf1.Rchg, 1, env.Xdf1.Rchg, line1, count1);
        Array.Copy(subEnv.Xdf2.Rchg, 1, env.Xdf2.Rchg, line2, count2);
    }

    internal static ReadOnlyMemory<byte> ExtractSubBuffer(XdFile xdf, int line, int count)
    {
        XdRecord firstRec = xdf.Recs[line - 1];
        XdRecord lastRec = xdf.Recs[line + count - 2];
        int startOff = firstRec.Offset;
        int endOff = lastRec.Offset + lastRec.Length;
        return xdf.Data[startOff..endOff];
    }
}
