/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.cli;
using System;
using System.Runtime.InteropServices;

namespace chibias;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(
                args,
                ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker);

            if (options.ShowHelp || options.SourceCodePaths.Count == 0)
            {
                Console.WriteLine();
                Options.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            using var logger = new TextWriterLogger(
                options.LogLevel,
                Console.Out);

            logger.Information($"Started. [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");

            options.Write(logger);

            var assembler = new Assembler(logger);

            if (assembler.Assemble(
                options.OutputAssemblyPath,
                options.AssemblerOptions,
                options.SourceCodePaths.ToArray()))
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
            Console.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Marshal.GetHRForException(ex);
        }
    }
}
