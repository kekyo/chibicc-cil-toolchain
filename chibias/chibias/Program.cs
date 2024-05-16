/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;
using chibias.Cli;
using chibicc.toolchain.Logging;

namespace chibias;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);

            if (options.ShowHelp || options.SourceFilePath == null)
            {
                Console.WriteLine();
                Console.WriteLine($"cil-ecma-chibias [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");
                Console.WriteLine("This is a CIL assembler, part of chibicc-cil project.");
                Console.WriteLine("https://github.com/kekyo/chibicc-cil-toolchain");
                Console.WriteLine("Copyright (c) Kouji Matsui");
                Console.WriteLine("License under MIT");
                Console.WriteLine();
                Console.WriteLine("usage: cil-ecma-chibias [options] <soruce path>");
                CliOptions.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            using var logger = new TextWriterLogger(
                options.LogLevel,
                Console.Out);

            logger.Information($"Started. [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");

            var assembler = new Assembler(logger);

            if (assembler.Assemble(
                options.OutputObjectFilePath,
                options.SourceFilePath,
                options.IsDryRun))
            {
                logger.Information($"Finished.");
                return 0;
            }
            else
            {
                logger.Information($"Failed assembling.");
                return 2;
            }
        }
        catch (InvalidOptionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return Marshal.GetHRForException(ex);
        }
    }
}
