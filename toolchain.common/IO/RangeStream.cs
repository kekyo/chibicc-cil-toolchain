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
using System.Threading;

namespace chibicc.toolchain.IO;

internal sealed class RangedStream : Stream
{
    private readonly bool leaveOpen;
    private Stream parent;
    private long position;

    public RangedStream(Stream parent, long length, bool leaveOpen)
    {
        this.parent = parent;
        this.Length = length;
        this.leaveOpen = leaveOpen;
    }

    public override bool CanRead =>
        true;
    public override bool CanSeek =>
        false;
    public override bool CanWrite =>
        false;

    public override long Length { get; }
    public override long Position
    {
        get => this.position;
        set => throw new InvalidOperationException();
    }

    public override void Close()
    {
        if (!this.leaveOpen)
        {
            if (Interlocked.Exchange(ref this.parent, null!) is { } parent)
            {
                parent.Dispose();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Close();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remains = (int)Math.Min(count, this.Length - this.position);
        if (remains == 0)
        {
            return 0;
        }

        var read = this.parent.Read(buffer, offset, remains);

        this.position += read;
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new InvalidOperationException();
    public override void SetLength(long value) =>
        throw new InvalidOperationException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException();
}
