﻿/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Text;
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
    
    public static TextReader CreateTextReader(Stream stream) =>
        new StreamReader(stream, CommonUtilities.UTF8, true);

    public static TextWriter CreateTextWriter(Stream stream) =>
        new StreamWriter(stream, CommonUtilities.UTF8);
}
