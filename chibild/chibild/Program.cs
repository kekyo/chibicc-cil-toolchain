/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Logging;
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

            if (options.ShowHelp || options.InputReferences.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine($"cil-ecma-chibild [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");
                Console.WriteLine("This is the CIL object linker, part of chibicc-cil project.");
                Console.WriteLine("https://github.com/kekyo/chibicc-cil-toolchain");
                Console.WriteLine("Copyright (c) Kouji Matsui");
                Console.WriteLine("License under MIT");
                Console.WriteLine();
                Console.WriteLine("usage: cil-ecma-chibild [options] <input path> [<input path> ...]");
                options.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            using var logger = new TextWriterLogger(
                options.LogLevel,
                Console.Out);

            logger.Information($"Started. [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");

            options.WriteOptions(logger);

            var linker = new CilLinker(logger);

            if (linker.Link(options))
            {
                logger.Information("Finished.");
                return 0;
            }
            else
            {
                logger.Information("Failed linking.");
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
