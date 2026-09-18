// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff.Emit;

internal sealed class StructuredSink : IHunkSink
{
    private readonly List<DiffHunk> _hunks = [];
    private readonly List<DiffLine> _lines = [];
    private bool _started;
    private int _oldStart;
    private int _oldCount;
    private int _newStart;
    private int _newCount;
    private ReadOnlyMemory<byte> _func;

    public void HunkHeader(int s1, int c1, int s2, int c2, ReadOnlyMemory<byte> func)
    {
        if (_started)
        {
            Flush();
        }

        _oldStart = c1 != 0 ? s1 + 1 : s1;
        _oldCount = c1;
        _newStart = c2 != 0 ? s2 + 1 : s2;
        _newCount = c2;
        _func = func;
        _lines.Clear();
        _started = true;
    }

    public void Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine)
        => _lines.Add(new DiffLine(kind, oldLine, newLine, content));

    public DiffResult ToResult()
    {
        if (_started)
        {
            Flush();
        }

        return new DiffResult(_hunks);
    }

    private void Flush()
        => _hunks.Add(new DiffHunk(_oldStart, _oldCount, _newStart, _newCount, _func, [.. _lines]));
}
