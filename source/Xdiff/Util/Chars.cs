// Copyright (C) 2026 Yi Jin
// SPDX-License-Identifier: LGPL-2.1-or-later
// See LICENSE for license terms and the disclaimer of warranty.

namespace Xdiff.Util;

internal static class Chars
{
    internal static bool IsSpace(byte c) => (char)c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';
}
