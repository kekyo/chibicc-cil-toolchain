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
                CliOptions.WriteUsage(Console.Out);
                Console.WriteLine();
                return 1;
            }

            var archiver = new Archiver();

            switch (options.Mode)
            {
                case ArchiveModes.Add:
                    if (archiver.Add(
                        options.ArchiveFilePath,
                        options.SymbolTableMode,
                        options.ObjectFilePaths.ToArray(),
                        options.IsDryRun) == AddResults.Created &&
                        !options.IsSilent)
                    {
                        Console.WriteLine($"cil-chibiar: creating {Path.GetFileName(options.ArchiveFilePath)}");
                    }
                    break;
            }

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
