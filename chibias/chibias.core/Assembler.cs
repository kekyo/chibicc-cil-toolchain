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
using chibicc.toolchain.Logging;

namespace chibias;

public sealed class Assembler
{
    private readonly ILogger logger;

    public Assembler(ILogger logger) =>
        this.logger = logger;

    public bool Assemble(
        string outputObjectFilePath,
        string sourceFilePath,
        bool isDryrun)
    {
        using var outputStream = isDryrun ?
            null : StreamUtilities.OpenStream(outputObjectFilePath, true);

        using var inputStream = StreamUtilities.OpenStream(sourceFilePath, false);

        if (outputStream != null)
        {
            inputStream.CopyTo(outputStream);
            outputStream.Flush();
        }

        return false;
    }
}
