/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using System.Collections.Generic;
using System.IO;

namespace chibicc.toolchain.IO;

internal sealed class LittleEndianReader
{
    private readonly Stream parent;
    
    public LittleEndianReader(Stream parent) =>
        this.parent = parent;

    public long? Position =>
        this.parent.CanSeek ? this.parent.Position : null;

    private int ReadByte() =>
        this.parent.ReadByte();

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
        ushort v = 0;
        for (var i = 0; i < sizeof(short); i++)
        {
            var ch = this.ReadByte();
            if (ch == -1)
            {
                value = 0;
                return false;
            }
            v |= (ushort)(((ushort)ch) << (i * 8));
        }
        value = (short)v;
        return true;
    }

    public bool TryReadInt32(out int value)
    {
        uint v = 0;
        for (var i = 0; i < sizeof(int); i++)
        {
            var ch = this.ReadByte();
            if (ch == -1)
            {
                value = 0;
                return false;
            }
            v |= (uint)(((uint)ch) << (i * 8));
        }
        value = (int)v;
        return true;
    }

    public bool TryReadInt64(out long value)
    {
        ulong v = 0;
        for (var i = 0; i < sizeof(long); i++)
        {
            var ch = this.ReadByte();
            if (ch == -1)
            {
                value = 0;
                return false;
            }
            v |= (ulong)(((ulong)ch) << (i * 8));
        }
        value = (long)v;
        return true;
    }

    public bool TryReadString(int maxLength, out string value)
    {
        if (maxLength >= 1)
        {
            var buffer = new List<byte>(maxLength);
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
        }
        value = null!;
        return false;
    }
}
