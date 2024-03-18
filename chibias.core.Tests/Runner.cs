/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using chibias.Internal;

namespace chibias;

partial class AssemblerTests
{
    private readonly string id =
        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{new Random().Next()}";

    private string Run(
        string[] chibiasSourceCodes,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        string[]? additionalReferencePaths = null,
        string targetFrameworkMoniker = "net45",
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

            logger.Information($"Test runner BasePath={basePath}");

            try
            {
                var coreLibPath = Path.GetFullPath("mscorlib.dll");
                var tmp2Path = Path.GetFullPath("tmp2.dll");
                var appHostTemplatePath = Path.GetFullPath(
                    Utilities.IsInWindows ? "apphost.exe" : "apphost.linux-x64");

                var referenceAssemblyBasePaths = new[]
                    {
                        coreLibPath, tmp2Path,
                    }.
                    Concat(additionalReferencePaths ?? Array.Empty<string>()).
                    Select(Utilities.GetDirectoryPath).
                    Distinct().
                    ToArray();
                var referenceAssemblyNames = new[]
                    {
                        coreLibPath, tmp2Path,
                    }.
                    Concat(additionalReferencePaths ?? Array.Empty<string>()).
                    Select(Path.GetFileNameWithoutExtension).
                    Distinct().
                    ToArray();

                var assember = new Assembler(logger);

                var outputAssemblyPath =
                    Path.Combine(basePath, "output.dll");
                var tf = TargetFramework.TryParse(targetFrameworkMoniker, out var tf1) ?
                    tf1 : throw new InvalidOperationException();
                var succeeded = assember.Assemble(
                    outputAssemblyPath,
                    new()
                    {
                        ReferenceAssemblyBasePaths = referenceAssemblyBasePaths,
                        ReferenceAssemblyNames = referenceAssemblyNames!,
                        DebugSymbolType = DebugSymbolTypes.Embedded,
                        IsDeterministic = true,
                        CreationOptions = new()
                        {
                            Options = AssembleOptions.None,
                            AssemblyType = assemblyType,
                            TargetFramework = tf,
                            AppHostTemplatePath = appHostTemplatePath,
                        },
                    },
                    chibiasSourceCodes.Select((sc, index) =>
                        new SourceCodeItem(new StringReader(sc), index >= 1 ? $"source{index}.s" : "source.s")).
                        ToArray());

                var disassembledPath =
                    Path.Combine(basePath, "output.il");

                var psi = new ProcessStartInfo()
                {
                    FileName = Path.GetFullPath(
                        Utilities.IsInWindows ? "ildasm.exe" : "ildasm.linux-x64"),
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

    private string Run(
        string chibiasSourceCode,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        string[]? additionalReferencePaths = null,
        string targetFrameworkMoniker = "net45",
        [CallerMemberName] string memberName = null!) =>
        this.Run(
            new[] { chibiasSourceCode },
            assemblyType,
            additionalReferencePaths,
            targetFrameworkMoniker,
            memberName);
}
