/*
 *  LibXDiff by Davide Libenzi ( File Differential Library )
 *  Copyright (C) 2003  Davide Libenzi
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

using Xdiff.Util;

namespace Xdiff.Prepare;

internal sealed class XdClassifier(WhitespaceMode flags, int estimatedClasses)
{
    private readonly Dictionary<ulong, int> _classes = new(estimatedClasses);

    private readonly List<XdClass> _records = new(estimatedClasses);

    public int Count => _records.Count;

    public XdClass GetClass(int idx) => _records[idx];

    public XdClass ClassifyRecord(int pass, ReadOnlyMemory<byte> line, ulong rawHash)
    {
        ReadOnlySpan<byte> lineSpan = line.Span;

        int head = _classes.TryGetValue(rawHash, out int index) ? index : -1;

        // Hash buckets are chains of indices into the record storage. Keep
        // comparing bytes: distinct line classes can share the same hash.
        for (int idx = head; idx >= 0; idx = _records[idx].Next)
        {
            XdClass c = _records[idx];
            if (RecordMatch.RecordsEqual(c.Line.Span, lineSpan, flags))
            {
                _records[idx] = pass == 1
                    ? c with { Len1 = c.Len1 + 1 }
                    : c with { Len2 = c.Len2 + 1 };
                return _records[idx];
            }
        }

        var newClass = new XdClass
        {
            Ha = rawHash,
            Next = head,
            Line = line,
            Idx = _records.Count,
            Len1 = pass == 1 ? 1 : 0,
            Len2 = pass == 1 ? 0 : 1,
        };

        _classes[rawHash] = _records.Count;
        _records.Add(newClass);
        return newClass;
    }
}
