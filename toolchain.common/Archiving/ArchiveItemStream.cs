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
using System.IO.Compression;

namespace chibicc.toolchain.Archiving;

internal sealed class ArchiveItemStream : Stream
{
    // Automatic closer for parent ZipArchive.
    
    private readonly ZipArchive archive;
    private readonly Stream parent;
        
    public ArchiveItemStream(ZipArchive archive, Stream parent)
    {
        this.archive = archive;
        this.parent = parent;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        this.parent.Dispose();
        this.archive.Dispose();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotImplementedException();

    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        this.parent.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new System.NotImplementedException();

    public override void SetLength(long value) =>
        throw new System.NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new System.NotImplementedException();
}
