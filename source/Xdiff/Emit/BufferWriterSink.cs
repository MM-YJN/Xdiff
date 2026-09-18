// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

using System.Buffers;

namespace Xdiff.Emit;

/// <summary>
/// Writes unified-diff text into a caller-supplied <see cref="IBufferWriter{T}" />.
/// </summary>
/// <remarks>
/// The hunk-header/line rendering below mirrors <see cref="StringSink" /> byte-for-byte and must be
/// kept in sync with it. (A future cleanup could have <c>StringSink</c> delegate to this type, since
/// its writer is itself an <see cref="IBufferWriter{T}" />.)
/// </remarks>
internal sealed class BufferWriterSink(IBufferWriter<byte> writer) : IHunkSink
{
    public void HunkHeader(int s1, int c1, int s2, int c2, ReadOnlyMemory<byte> func)
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

        Span<byte> span;
        if (!func.IsEmpty)
        {
            span = writer.GetSpan(func.Length + 1);
            span[0] = (byte)' ';
            func.Span.CopyTo(span.Slice(1));
            writer.Advance(func.Length + 1);
        }

        span = writer.GetSpan(1);
        span[0] = (byte)'\n';
        writer.Advance(1);
    }

    public void Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine)
    {
        ReadOnlySpan<byte> data = content.Span;
        bool needsEofnlMarker = !data.IsEmpty && data[^1] != '\n';

        Span<byte> span = writer.GetSpan(data.Length + 1);

        span[0] = kind switch
        {
            DiffLineKind.Context => (byte)' ',
            DiffLineKind.Addition => (byte)'+',
            DiffLineKind.Deletion => (byte)'-',
            _ => (byte)' ',
        };
        data.CopyTo(span.Slice(1));
        writer.Advance(data.Length + 1);

        // The EOFNL probe reads the logical content span, not the writer's span: GetSpan(n) may
        // return a span larger than n, whose tail holds unrelated pooled bytes.
        if (needsEofnlMarker)
        {
            writer.Write("\n\\ No newline at end of file\n"u8);
        }
    }

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

        writer.Write(bytes);
    }
}
