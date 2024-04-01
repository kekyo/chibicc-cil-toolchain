/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibild.cli;
using System;
using System.Runtime.InteropServices;

namespace chibild;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(
                args,
                ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker);

            if (options.ShowHelp || options.ObjectFilePaths.Count == 0)
            {
                Console.WriteLine();
                CliOptions.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            using var logger = new TextWriterLogger(
                options.LogLevel,
                Console.Out);

            logger.Information($"Started. [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");

            options.Write(logger);

            var linker = new Linker(logger);

            if (linker.Link(
                options.OutputAssemblyPath,
                options.LinkerOptions,
                options.InjectToAssemblyPath,
                options.ObjectFilePaths.ToArray()))
            {
                logger.Information($"Finished.");
                return 0;
            }
            else
            {
                logger.Information($"Failed linking.");
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
