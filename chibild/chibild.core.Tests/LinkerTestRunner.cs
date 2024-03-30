/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibild.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace chibild;

internal static class LinkerTestRunner
{
    public static readonly string ArtifactsBasePath = Path.GetFullPath(
        Path.Combine("..", "..", "..", "artifacts"));
    
    private static readonly string id =
        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{new Random().Next()}";

    public static string RunCore(
        string[] chibildSourceCodes,
        string[]? additionalReferencePaths,
        string? injectToAssemblyPath,
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
                var coreLibPath = Path.Combine(ArtifactsBasePath, "mscorlib.dll");
                var tmp2Path = Path.Combine(ArtifactsBasePath, "tmp2.dll");

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

                var assember = new Linker(logger);

                var outputAssemblyPath =
                    Path.Combine(basePath, "output.dll");

                if (injectToAssemblyPath != null)
                {
                    File.Copy(injectToAssemblyPath, outputAssemblyPath, true);
                }

                var succeeded = assember.Link(
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
                    chibildSourceCodes.Select((sc, index) =>
                        new ObjectFileItem(new StringReader(sc), index >= 1 ? $"source{index}.s" : "source.s")).
                        ToArray());

                var disassembledPath =
                    Path.Combine(basePath, "output.il");

                var psi = new ProcessStartInfo()
                {
                    FileName = Path.Combine(ArtifactsBasePath,
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
