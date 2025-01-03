/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace chibicc.toolchain.IO;

internal sealed class NullStream : Stream
{
    public NullStream()
    {
    }

    public override bool CanRead =>
        true;
    public override bool CanSeek =>
        false;
    public override bool CanWrite =>
        true;

    public override long Length =>
        throw new InvalidOperationException();

    public override long Position
    {
        get => throw new InvalidOperationException();
        set => throw new InvalidOperationException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        0;

    public override void Write(byte[] buffer, int offset, int count)
    {
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();
    public override void SetLength(long value) =>
        throw new InvalidOperationException();
}
