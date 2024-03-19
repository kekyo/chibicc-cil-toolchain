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
using System.Linq;
using System.Text;

namespace chibias;

internal static class AssemblerTestRunner
{
    private static readonly string artifactsBasePath = Path.GetFullPath("artifacts");
    private static readonly string id =
        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{new Random().Next()}";

    public static string RunCore(
        string[] chibiasSourceCodes,
        string[]? additionalReferencePaths,
        string? mergeAssemblyPath,
        Func<AssemblerCreationOptions?> creationOptions,
        string memberName)
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
                var coreLibPath = Path.Combine(artifactsBasePath, "mscorlib.dll");
                var tmp2Path = Path.Combine(artifactsBasePath, "tmp2.dll");

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

                if (mergeAssemblyPath != null)
                {
                    File.Copy(mergeAssemblyPath, outputAssemblyPath, true);
                }

                var succeeded = assember.Assemble(
                    outputAssemblyPath,
                    new()
                    {
                        ReferenceAssemblyBasePaths = referenceAssemblyBasePaths,
                        ReferenceAssemblyNames = referenceAssemblyNames!,
                        DebugSymbolType = DebugSymbolTypes.Embedded,
                        IsDeterministic = true,
                        ApplyOptimization = false,
                        CreationOptions = creationOptions(),
                    },
                    chibiasSourceCodes.Select((sc, index) =>
                        new SourceCodeItem(new StringReader(sc), index >= 1 ? $"source{index}.s" : "source.s")).
                        ToArray());

                var disassembledPath =
                    Path.Combine(basePath, "output.il");

                var psi = new ProcessStartInfo()
                {
                    FileName = Path.Combine(artifactsBasePath,
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
                        !line.StartsWith("// MVID:") &&
                        !line.StartsWith("// WARNING: Created Win32 resource file"))
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
