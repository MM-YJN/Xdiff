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

using Xdiff.Util;

namespace Xdiff.Emit;

internal sealed class StringSink : IHunkSink, IDisposable
{
    private readonly PooledByteBufferWriter _writer = new();

    public void HunkHeader(int s1, int c1, int s2, int c2, ReadOnlySpan<byte> func)
    {
        int oldStart = c1 != 0 ? s1 + 1 : s1;
        int newStart = c2 != 0 ? s2 + 1 : s2;

        Span<char> hdr = stackalloc char[128];
        int n = 0;
        Append(hdr, ref n, "@@ -");
        n += UnifiedDiffEmitter.FormatNum(hdr[n..], oldStart);
        if (c1 != 1)
        {
            hdr[n++] = ',';
            n += UnifiedDiffEmitter.FormatNum(hdr[n..], c1);
        }

        Append(hdr, ref n, " +");
        n += UnifiedDiffEmitter.FormatNum(hdr[n..], newStart);
        if (c2 != 1)
        {
            hdr[n++] = ',';
            n += UnifiedDiffEmitter.FormatNum(hdr[n..], c2);
        }

        Append(hdr, ref n, " @@");
        WriteAscii(hdr[..n]);

        if (!func.IsEmpty)
        {
            _writer.Write((byte)' ');
            _writer.Write(func);
        }

        _writer.Write((byte)'\n');
    }

    public void Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine)
    {
        _ = oldLine;
        _ = newLine;
        ReadOnlySpan<byte> span = content.Span;
        _writer.Write(kind switch
        {
            DiffLineKind.Context => (byte)' ',
            DiffLineKind.Addition => (byte)'+',
            DiffLineKind.Deletion => (byte)'-',
            _ => (byte)' ',
        });

        _writer.Write(span);
        if (!span.IsEmpty && span[^1] != '\n')
        {
            _writer.Write("\n\\ No newline at end of file\n"u8);
        }
    }

    public byte[] ToBytes() => _writer.WrittenMemory.ToArray();

    public void Dispose() => _writer.Dispose();

    private static void Append(Span<char> buf, ref int n, string s)
    {
        foreach (char ch in s)
        {
            buf[n++] = ch;
        }
    }

    private void WriteAscii(ReadOnlySpan<char> chars)
    {
        Span<byte> bytes = stackalloc byte[chars.Length];
        for (int i = 0; i < chars.Length; i++)
        {
            bytes[i] = (byte)chars[i];
        }

        _writer.Write(bytes);
    }
}
