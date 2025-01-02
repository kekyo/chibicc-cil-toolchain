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
using chibicc.toolchain.Internal;

namespace chibicc.toolchain.IO;

public static class StreamUtilities
{
    public static Stream OpenStream(string path, bool writable) =>
        (path == "-") ?
            (writable ? Console.OpenStandardOutput() : Console.OpenStandardInput()) :
            new FileStream(
                path,
                writable ? FileMode.Create : FileMode.Open,
                writable ? FileAccess.ReadWrite : FileAccess.Read,
                FileShare.Read,
                1024 * 1024);

    public static TextWriter CreateTextWriter(Stream stream)
    {
        var s = new StreamWriter(stream, CommonUtilities.UTF8);
        s.NewLine = "\n";
        return s;
    }
    
    public static TextReader CreateTextReader(Stream stream) =>
        new StreamReader(stream, CommonUtilities.UTF8, true);
    
    public static BinaryWriter CreateBinaryWriter(Stream stream) =>
        new(stream, CommonUtilities.UTF8);
    
    public static BinaryReader CreateBinaryReader(Stream stream) =>
        new(stream, CommonUtilities.UTF8);
}
