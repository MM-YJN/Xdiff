# Third-party notices

Xdiff is a C# port of [libgit2/xdiff](https://github.com/libgit2/xdiff).
The LibXDiff, histogram, and PooledByteBufferWriter notices below accompany its
binary distributions.
The test-fixture section separately attributes resources used by the test suite.

## LibXDiff

LibXDiff by Davide Libenzi (File Differential Library).
Copyright notices retained in the ported source include:

- Copyright (C) 2003 Davide Libenzi
- Copyright (C) 2003-2006 Davide Libenzi, Johannes E. Schindelin
- Copyright (C) 2003-2016 Davide Libenzi, Johannes E. Schindelin

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
Lesser General Public License for more details.

The full LGPL text is included in the accompanying `LICENSE` file.

Davide Libenzi <davidel@xmailserver.org>

## Histogram implementation

The histogram implementation is derived from
[xhistogram.c](https://raw.githubusercontent.com/libgit2/xdiff/main/xhistogram.c).
Its Eclipse Distribution License v1.0 uses the SPDX identifier `BSD-3-Clause`.
The complete upstream notice follows:

```text
Copyright (C) 2010, Google Inc.
and other copyright owners as documented in JGit's IP log.

This program and the accompanying materials are made available
under the terms of the Eclipse Distribution License v1.0 which
accompanies this distribution, is reproduced below, and is
available at http://www.eclipse.org/org/documents/edl-v10.php

All rights reserved.

Redistribution and use in source and binary forms, with or
without modification, are permitted provided that the following
conditions are met:

- Redistributions of source code must retain the above copyright
  notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above
  copyright notice, this list of conditions and the following
  disclaimer in the documentation and/or other materials provided
  with the distribution.

- Neither the name of the Eclipse Foundation, Inc. nor the
  names of its contributors may be used to endorse or promote
  products derived from this software without specific prior
  written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## PooledByteBufferWriter

The `PooledByteBufferWriter` helper is licensed under MIT. Its complete notice
follows:

```text
Copyright (C) 2026 Yi Jin
SPDX-License-Identifier: MIT

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Test fixtures (test resources only)

The test suite includes fixtures copied or derived from libgit2 resources,
covered by libgit2's GPLv2 notice with its linking exception, alongside project
test inputs and reference-generated outputs. See
[fixture provenance](tests/Xdiff.UnitTests/FIXTURE-NOTICES.md) for source paths,
verified Git object IDs, and generation details, and
[the libgit2 fixture license](tests/Xdiff.UnitTests/LICENSE.libgit2.txt) for the
verbatim upstream copyright statement, linking exception, and GPLv2 text.
These test resources are not included in the Xdiff library package.
