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

namespace chibicc.toolchain.IO;

public static class ObjectStreamUtilities
{
    public static Stream OpenObjectStream(string objectFilePath, bool writable)
    {
        var s = StreamUtilities.OpenStream(objectFilePath, writable);
        if (Path.GetExtension(objectFilePath) is not ".s")
        {
            return writable ?
                new GZipStream(s, CompressionLevel.Optimal) :
                new GZipStream(s, CompressionMode.Decompress);
        }
        else
        {
            return s;
        }
    }
}
