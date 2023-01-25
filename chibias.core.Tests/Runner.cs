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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        string[]? additionalReferencePaths = null,
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

            logger.Information($"Test runnner BasePath={basePath}");

            try
            {
                var corlibPath = typeof(object).Assembly.Location;
                var tmp2Path = Path.GetFullPath("tmp2.dll");

                var referenceAssemblyBasePaths = new[]
                {
                    //corlibPath,
                    tmp2Path,
                }.
                    Concat(additionalReferencePaths ?? Array.Empty<string>()).
                    Select(Utilities.GetDirectoryPath).
                    Distinct().
                    ToArray();
                var referenceAssemblyPaths = new[]
                {
                    //corlibPath,
                    tmp2Path,
                }.
                    Concat(additionalReferencePaths ?? Array.Empty<string>()).
                    ToArray();

                var assember = new Assembler(
                    logger,
                    referenceAssemblyBasePaths);

                var outputAssemblyPath =
                    Path.Combine(basePath, "output.dll");
                var succeeded = assember.Assemble(
                    outputAssemblyPath,
                    new()
                    {
                        ReferenceAssemblyPaths = referenceAssemblyPaths,
                        AssemblyType = assemblyType,
                        TargetFrameworkMoniker = "net48",
                    },
                    "source.s",
                    new StringReader(chibiasSourceCode));

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
                    logger.Error($"Failed to run assembler.");

                    logtw.Flush();
                    logfs.Close();

                    return File.ReadAllText(logPath);
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
                try
                {
                    logtw.Flush();
                }
                catch
                {
                }
            }
        }

        return disassembledSourceCode.ToString();
    }
}
