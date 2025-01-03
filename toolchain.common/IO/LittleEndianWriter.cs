/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using chibicc.toolchain.Internal;

namespace chibicc.toolchain.IO;

internal sealed class LittleEndianWriter
{
    private readonly Stream parent;
    private readonly byte[] buffer = new byte[8];

    public LittleEndianWriter(Stream parent) =>
        this.parent = parent;

    public void Write(byte value)
    {
        this.buffer[0] = value;
        this.parent.Write(this.buffer, 0, sizeof(byte));
    }

    public void Write(short value)
    {
        for (var i = 0; i < sizeof(short); i++)
        {
            this.buffer[i] = (byte)value;
            value >>= 8;
        }
        this.parent.Write(this.buffer, 0, sizeof(short));
    }

    public void Write(int value)
    {
        for (var i = 0; i < sizeof(int); i++)
        {
            this.buffer[i] = (byte)value;
            value >>= 8;
        }
        this.parent.Write(this.buffer, 0, sizeof(int));
    }

    public void Write(long value)
    {
        for (var i = 0; i < sizeof(long); i++)
        {
            this.buffer[i] = (byte)value;
            value >>= 8;
        }
        this.parent.Write(this.buffer, 0, sizeof(long));
    }

    public void Write(string value)
    {
        var bytes = CommonUtilities.UTF8.GetBytes(value);
        this.parent.Write(bytes, 0, bytes.Length);
        this.buffer[0] = 0;
        this.parent.Write(bytes, 0, sizeof(byte));
    }

    public void Write(byte[] buffer, int offset, int length) =>
        this.parent.Write(buffer, offset, length);

    public void Flush() =>
        this.parent.Flush();
}
