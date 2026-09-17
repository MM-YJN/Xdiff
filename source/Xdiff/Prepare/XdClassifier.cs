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
    private readonly Dictionary<ulong, List<int>> _classes = new(estimatedClasses);

    private readonly List<XdClass> _records = new(estimatedClasses);

    public int Count => _records.Count;

    public XdClass GetClass(int idx) => _records[idx];

    public XdClass ClassifyRecord(int pass, ReadOnlyMemory<byte> line, ulong rawHash)
    {
        ReadOnlySpan<byte> lineSpan = line.Span;

        if (!_classes.TryGetValue(rawHash, out List<int>? bucket))
        {
            bucket = [];
            _classes[rawHash] = bucket;
        }

        // Buckets hold indices into _records: XdClass is now a struct, so
        // updating Len1/Len2 requires a with-copy written back to the same
        // slot (a foreach over the structs would mutate copies).
        foreach (int idx in bucket)
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
            Line = line,
            Idx = _records.Count,
            Len1 = pass == 1 ? 1 : 0,
            Len2 = pass == 1 ? 0 : 1,
        };

        bucket.Add(_records.Count);
        _records.Add(newClass);
        return newClass;
    }
}
