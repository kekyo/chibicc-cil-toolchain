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

internal sealed class StreamDemuxer
{
    private static byte[]? dummy;
    
    private readonly LittleEndianReader reader;
    private readonly Dictionary<int, bool> ids = new();

    public StreamDemuxer(Stream targetStream)
    {
        this.reader = new LittleEndianReader(targetStream);

        if (!this.reader.TryReadString(9, out var header))
        {
            throw new FormatException("Invalid archive format [1].");
        }
        if (header != ArchiverUtilities.HeadIdentity)
        {
            throw new FormatException("Invalid archive format [2].");
        }
    }

    public record Entry(
        int Id,
        int Length,
        string? Name);

    public Entry? Prepare()
    {
        if (this.reader.IsEos)
        {
            return null;
        }
        if (!this.reader.TryReadInt32(out var id))
        {
            throw new FormatException("Invalid archive format [3].");
        }
        if (!this.reader.TryReadInt32(out var length))
        {
            throw new FormatException("Invalid archive format [4].");
        }
        if (!this.ids.TryGetValue(id, out var fetch))
        {
            if (!this.reader.TryReadString(256, out var name))
            {
                throw new FormatException("Invalid archive format [5].");
            }
            this.ids.Add(id, fetch);
            return new(id, length, name);
        }
        return new(id, length, null);
    }

    public byte[] Read(Entry entry)
    {
        var buffer = new byte[entry.Length];
        var read = this.reader.Read(buffer, buffer.Length);
        if (read != buffer.Length)
        {
            throw new FormatException("Invalid archive format [6].");
        }
        return buffer;
    }

    public void Skip(Entry entry)
    {
        if (dummy == null)
        {
            dummy = new byte[65536];
        }
        var length = entry.Length;
        while (length >= 1)
        {
            var skipLength = Math.Min(entry.Length, dummy.Length);
            var read = this.reader.Read(dummy, skipLength);
            if (read != skipLength)
            {
                throw new FormatException("Invalid archive format [7].");
            }
            length -= skipLength;
        }
    }

    public void Finish(Entry entry) =>
        this.ids.Remove(entry.Id);
}

internal static class StreamDemuxerExtension
{
    public static void Run(
        this StreamDemuxer demuxer,
        Func<int, string, bool> predicate,
        Action<int, byte[]> action,
        Func<int, bool> finished)
    {
        var ids = new Dictionary<int, bool>();
        while (true)
        {
            if (demuxer.Prepare() is not { } entry)
            {
                break;
            }
            bool fetch;
            if (entry.Name is { } name)
            {
                fetch = predicate(entry.Id, name);
                ids.Add(entry.Id, fetch);
            }
            else
            {
                if (!ids.TryGetValue(entry.Id, out fetch))
                {
                    throw new FormatException("Invalid archive format.");
                }
            }
            if (entry.Length >= 1)
            {
                if (fetch)
                {
                    var buffer = demuxer.Read(entry);
                    action(entry.Id, buffer);
                }
                else
                {
                    demuxer.Skip(entry);
                }
            }
            else
            {
                finished(entry.Id);
                demuxer.Finish(entry);
                ids.Remove(entry.Id);
            }
        }
    }
}
