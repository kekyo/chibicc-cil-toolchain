/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Runtime.InteropServices;
using chibiar.cli;
using chibicc.toolchain.Logging;

namespace chibiar;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);

            if (options.ShowHelp || options.ObjectFilePaths.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine($"cil-ecma-chibiar [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");
                Console.WriteLine("This is a CIL object archiver, part of chibicc-cil project.");
                Console.WriteLine("https://github.com/kekyo/chibicc-cil-toolchain");
                Console.WriteLine("Copyright (c) Kouji Matsui");
                Console.WriteLine("License under MIT");
                Console.WriteLine();
                Console.WriteLine("usage: cil-ecma-chibiar [options] <archive path> [<obj path> ...]");
                CliOptions.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            using var logger = new TextWriterLogger(
                options.LogLevel,
                Console.Out);

            logger.Information($"Started. [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");

            var archiver = new Archiver(logger);

            archiver.Archive(options);

            return 0;
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
