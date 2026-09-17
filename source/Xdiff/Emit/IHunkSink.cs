// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff.Emit;

internal interface IHunkSink
{
    void HunkHeader(int s1, int c1, int s2, int c2, ReadOnlySpan<byte> func);

    void Line(DiffLineKind kind, ReadOnlyMemory<byte> content, int oldLine, int newLine);
}
