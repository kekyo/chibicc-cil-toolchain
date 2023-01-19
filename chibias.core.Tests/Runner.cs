/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace chibias.core.Tests;

partial class AssemblerTests
{
    private readonly string id =
        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{new Random().Next()}";

    private string Run(
        string chibiasSourceCode,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        [CallerMemberName] string memberName = null!)
    {
        var basePath = Path.GetFullPath(
            Path.Combine("tests", id, memberName));

        Directory.CreateDirectory(basePath);

        var logPath = Path.Combine(basePath, "log.txt");

        var disassembledSourceCode = new StringBuilder();
        using (var logfs = new FileStream(
            logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var logtw = new StreamWriter(
                logfs, Encoding.UTF8);
            var logger = new TextWriterLogger(
                LogLevels.Debug, logtw);

            try
            {
                var sourcePath = Path.Combine(
                    basePath, "source.s");
                using (var tw = File.CreateText(sourcePath))
                {
                    tw.Write(chibiasSourceCode);
                    tw.Flush();
                }

                var tmp2Path = Path.GetFullPath("tmp2.dll");
                var tmp2BasePath = Utilities.GetDirectoryPath(tmp2Path);

                var assember = new Assembler(
                    logger,
                    tmp2BasePath);

                var outputAssemblyPath =
                    Path.Combine(basePath, "output.dll");
                var succeeded = assember.Assemble(
                    new[] { sourcePath },
                    outputAssemblyPath,
                    new[] { tmp2Path },
                    assemblyType,
                    DebugSymbolTypes.Embedded,
                    AssembleOptions.Deterministic,
                    new Version(1, 0, 0),
                    "net48");

                var disassembledPath =
                    Path.Combine(basePath, "output.il");

                var psi = new ProcessStartInfo()
                {
                    // Testing expected content is required MS's ILDAsm format,
                    // so unfortunately runs on Windows...
                    FileName = Path.GetFullPath("ildasm.exe"),
                    Arguments = $"-utf8 -out={disassembledPath} {outputAssemblyPath}"
                };

                using (var ildasm = Process.Start(psi)!)
                {
                    ildasm.WaitForExit();
                }

                if (!succeeded)
                {
                    throw new FormatException($"Failed assembling, see {basePath}");
                }

                using var disassembledReader = File.OpenText(disassembledPath);
                while (true)
                {
                    var line = disassembledReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    if (!line.StartsWith("// Image base:") &&
                        !line.StartsWith("// MVID:"))
                    {
                        disassembledSourceCode.AppendLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
            finally
            {
                logtw.Flush();
            }
        }

        return disassembledSourceCode.ToString();
    }
}
