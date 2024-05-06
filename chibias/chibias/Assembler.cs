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
using chibicc.toolchain.IO;

namespace chibias;

public sealed class Assembler
{
    public bool Assemble(
        string outputObjectFilePath,
        string[] sourceFilePaths,
        bool isDryrun)
    {
        using var outputStream = isDryrun ?
            null : StreamUtilities.OpenStream(outputObjectFilePath, true);

        foreach (var sourceFilePath in sourceFilePaths)
        {
            using var inputStream = StreamUtilities.OpenStream(sourceFilePath, false);

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