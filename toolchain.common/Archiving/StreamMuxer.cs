/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using chibicc.toolchain.IO;

namespace chibicc.toolchain.Archiving;

internal sealed class StreamMuxer
{
    private record struct SubStreamEntry(
        string Name, SubStream Stream);

    private readonly LittleEndianWriter writer;
    private readonly Dictionary<int, SubStreamEntry> streams = new();
    private int id;

    public StreamMuxer(Stream targetStream)
    {
        this.writer = new(targetStream);
        this.writer.Write(ArchiverUtilities.HeadIdentity);
    }

    public Stream CreateSubStream(string name)
    {
        lock (this.streams)
        {
            var id = this.id++;
            var subStream = new SubStream(this, id);
            this.streams.Add(id, new(name, subStream));
            return subStream;
        }
    }

    private void Write(int id, byte[] buffer, int offset, int count)
    {
        lock (this.streams)
        {
            if (this.streams.TryGetValue(id, out var entry))
            {
                this.writer.Write(id);
                this.writer.Write(count);
                if (!entry.Stream.IsWritten)
                {
                    this.writer.Write(entry.Name);
                }
                this.writer.Write(buffer, offset, count);
                this.writer.Flush();
            }
            else
            {
                throw new ObjectDisposedException(nameof(Stream));
            }
        }
    }

    private void Close(int id)
    {
        lock (this.streams)
        {
            if (this.streams.TryGetValue(id, out var entry))
            {
                this.streams.Remove(id);
                this.writer.Write(id);
                this.writer.Write(0);
                if (!entry.Stream.IsWritten)
                {
                    this.writer.Write(entry.Name);
                }
                this.writer.Flush();
            }
        }
    }

    private sealed class SubStream : Stream
    {
        private readonly StreamMuxer parent;
        private readonly int id;

        public SubStream(StreamMuxer parent, int id)
        {
            this.parent = parent;
            this.id = id;
        }

        public override bool CanRead =>
            false;
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

        public bool IsWritten
        {
            get;
            private set;
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return;
            }
            this.parent.Write(this.id, buffer, offset, count);
            this.IsWritten = true;
        }

        public override void Close()
        {
            base.Close();
            this.parent.Close(this.id);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new InvalidOperationException();
        public override void SetLength(long value) =>
            throw new InvalidOperationException();
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException();
    }
}
