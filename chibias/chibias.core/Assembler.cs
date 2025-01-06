/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;
using chibicc.toolchain.Parsing;
using chibicc.toolchain.Tokenizing;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        using var scope = this.logger.BeginScope(LogLevels.Debug);

        var outputTemporaryFilePath =
            Path.Combine(
                CommonUtilities.GetDirectoryPath(outputObjectFilePath),
                Guid.NewGuid().ToString("N"));
        var count = 0;
        
        var tasks = new[]
        {
            () =>
            {
                // Checking only parsing.
                using (var inputStream = StreamUtilities.OpenStream(sourceFilePath, false))
                {
                    var tr = StreamUtilities.CreateTextReader(inputStream);

                    var parser = new CilParser(this.logger);
                    var _ = parser.Parse(
                        CilTokenizer.TokenizeAll("", sourceFilePath, tr),
                        false).
                        ToArray();

                    if (!parser.CaughtError)
                    {
                        Interlocked.Increment(ref count);
                    }
                }
                
                scope.Debug("Checked");
            },
            () =>
            {
                // Convert source code to object file.
                using var outputStream = isDryrun ?
                    new NullStream() :
                    ObjectStreamUtilities.OpenObjectStream(outputTemporaryFilePath, true);

                using (var inputStream = StreamUtilities.OpenStream(sourceFilePath, false))
                {
                    inputStream.CopyTo(outputStream);
                    outputStream.Flush();
                }

                Interlocked.Increment(ref count);
                
                scope.Debug("Converted");
            },
        };

        try
        {
            Parallel.Invoke(tasks);
        }
        catch
        {
            try
            {
                File.Delete(outputTemporaryFilePath);
            }
            catch
            {
            }
            throw;
        }

        if (!isDryrun)
        {
            if (count == 2)
            {
                try
                {
                    File.Delete(outputObjectFilePath);
                }
                catch
                {
                }
                File.Move(outputTemporaryFilePath, outputObjectFilePath);
            }
            else
            {
                try
                {
                    File.Delete(outputTemporaryFilePath);
                }
                catch
                {
                }
            }
        }
        
        return count == 2;
    }
}
