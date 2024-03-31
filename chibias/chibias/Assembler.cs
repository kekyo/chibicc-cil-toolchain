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

namespace chibias;

public sealed class Assembler
{
    private static Stream OpenStream(string path, bool writable) =>
        (path == "-") ?
            (writable ? Console.OpenStandardOutput() : Console.OpenStandardInput()) :
            new FileStream(path, writable ? FileMode.Create : FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    
    public bool Assemble(
        string outputObjectFilePath,
        string[] sourceFilePaths,
        bool isDryrun)
    {
        using var outputStream = isDryrun ?
            null : OpenStream(outputObjectFilePath, true);

        foreach (var sourceFilePath in sourceFilePaths)
        {
            using var inputStream = OpenStream(sourceFilePath, false);

            if (outputStream != null)
            {
                inputStream.CopyTo(outputStream);
            }
        }

        if (outputStream != null)
        {
            outputStream.Flush();
        }

        return false;
    }
}