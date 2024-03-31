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
using chibias.cli;

namespace chibias;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);

            if (options.ShowHelp || options.SourceFilePaths.Count == 0)
            {
                Console.WriteLine();
                CliOptions.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            var assembler = new Assembler();

            if (assembler.Assemble(
                options.OutputObjectFilePath,
                options.SourceFilePaths.ToArray(),
                options.IsDryRun))
            {
                //logger.Information($"Finished.");
                return 0;
            }
            else
            {
                //logger.Information($"Failed linking.");
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
