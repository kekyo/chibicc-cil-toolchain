/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using chibicc.toolchain.Internal;

namespace chibicc.toolchain.IO;

internal sealed class LittleEndianReader
{
    private readonly Stream parent;
    private byte? preloaded;
    
    public LittleEndianReader(Stream parent) =>
        this.parent = parent;

    public bool IsEos
    {
        get
        {
            if (this.preloaded != null)
            {
                return false;
            }
            var ch = this.parent.ReadByte();
            if (ch == -1)
            {
                return true;
            }
            this.preloaded = (byte)ch;
            return false;
        }
    }

    private int ReadByte()
    {
        if (this.preloaded is { } preloaded)
        {
            this.preloaded = null;
            return preloaded;
        }
        return this.parent.ReadByte();
    }

    public bool TryReadByte(out byte value)
    {
        var ch = this.ReadByte();
        if (ch == -1)
        {
            value = 0;
            return false;
        }
        value = (byte)ch;
        return true;
    }

    public bool TryReadInt16(out short value)
    {
        value = 0;
        for (var i = 0; i < sizeof(short); i++)
        {
            var ch = this.ReadByte();
            if (ch == -1)
            {
                return false;
            }
            value |= (short)(ch << i);
        }
        return true;
    }

    public bool TryReadInt32(out int value)
    {
        value = 0;
        for (var i = 0; i < sizeof(int); i++)
        {
            var ch = this.ReadByte();
            if (ch == -1)
            {
                return false;
            }
            value |= ch << i;
        }
        return true;
    }

    public bool TryReadInt64(out long value)
    {
        value = 0;
        for (var i = 0; i < sizeof(long); i++)
        {
            var ch = this.ReadByte();
            if (ch == -1)
            {
                return false;
            }
            value |= (long)ch << i;
        }
        return true;
    }

    public bool TryReadString(int maxLength, out string value)
    {
        var buffer = new List<byte>(maxLength);
        if (this.preloaded is { } preloaded)
        {
            this.preloaded = null;
            buffer.Add(preloaded);
        }
        while (buffer.Count < maxLength)
        {
            var ch = this.parent.ReadByte();
            if (ch == -1)
            {
                value = null!;
                return false;
            }
            if (ch == 0)
            {
                value = CommonUtilities.UTF8.GetString(buffer.ToArray());
                return true;
            }
            buffer.Add((byte)ch);
        }
        value = null!;
        return false;
    }

    public int Read(byte[] buffer, int maxLength)
    {
        if (maxLength == 0)
        {
            return 0;
        }
        var position = 0;
        if (this.preloaded is { } preloaded)
        {
            this.preloaded = null;
            buffer[position++] = preloaded;
        }
        while (position < maxLength)
        {
            var read = this.parent.Read(buffer, position, maxLength - position);
            if (read == 0)
            {
                break;
            }
            position += read;
        }
        return position;
    }
}
