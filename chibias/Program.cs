/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace chibias;

public static class Program
{
    private static void WriteUsage(OptionSet options)
    {
        Console.WriteLine();
        Console.WriteLine($"chibias [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFramework}] [{ThisAssembly.AssemblyMetadata.CommitId}]");
        Console.WriteLine("This is the CIL assembler, part of chibicc-cil project.");
        Console.WriteLine("https://github.com/kekyo/chibias-cil");
        Console.WriteLine("Copyright (c) Kouji Matsui");
        Console.WriteLine("License under MIT");
        Console.WriteLine();
        Console.WriteLine($"usage: chibias [options] <source path> [<source path> ...]");

        options.WriteOptionDescriptions(Console.Out);
    }

    public static int Main(string[] args)
    {
        string? outputAssemblyPath = null;
        var referenceAssemblyPaths = new List<string>();
        var assemblyType = AssemblyTypes.Exe;
        var debugSymbolType = DebugSymbolTypes.Portable;
        var version = new Version(1, 0, 0, 0);
        var targetFrameworkMoniker = ThisAssembly.AssemblyMetadata.TargetFramework;
        var applyOptimization = false;
        var logLevel = LogLevels.Information;
        var doHelp = false;

        var options = new OptionSet
        {
            { "o=", "Output assembly path", v => outputAssemblyPath = v },
            { "c|dll", "Produce dll assembly", _ => assemblyType = AssemblyTypes.Dll },
            { "exe", "Produce executable assembly (defaulted)", _ => assemblyType = AssemblyTypes.Exe },
            { "winexe", "Produce Windows executable assembly", _ => assemblyType = AssemblyTypes.WinExe },
            { "r|reference=", "Reference assembly path", path => referenceAssemblyPaths.Insert(0, path) },  // HACK: same order for ld's library looking up
            { "g|g1|portable", "Produce portable debug symbol file (defaulted)", _ => debugSymbolType = DebugSymbolTypes.Portable },
            { "g0|no-debug", "Omit debug symbol file", _ => debugSymbolType = DebugSymbolTypes.None },
            { "g2|embedded", "Produce embedded debug symbol", _ => debugSymbolType = DebugSymbolTypes.Embedded },
            { "O", "Apply optimization", _ => applyOptimization = true },
            { "O0", "Disable optimization (defaulted)", _ => applyOptimization = false },
            { "asm-version=", "Apply assembly version", v => version = Version.Parse(v) },
            { "tfm=", $"Target framework moniker (defaulted: {ThisAssembly.AssemblyMetadata.TargetFramework})", v => targetFrameworkMoniker = v },
            { "log=", "Log level [debug|trace|information|warning|error|silent]", v => logLevel = Enum.TryParse<LogLevels>(v, true, out var ll) ? ll : LogLevels.Information },
            { "h|help", "Show this help", _ => doHelp = true },
        };

        try
        {
            var parsed = options.Parse(args);

            if (outputAssemblyPath == null)
            {
                outputAssemblyPath = assemblyType == AssemblyTypes.Dll ?
                    "a.out.dll" : "a.out.exe";
            }

            if (doHelp || (parsed.Count == 0))
            {
                WriteUsage(options);
                return 1;
            }

            using var logger = new TextWriterLogger(
                logLevel, Console.Out, ThisAssembly.AssemblyMetadata.TargetFramework);

            logger.Trace($"Started.");

            var sourcePaths = parsed.ToArray();
            var referenceAssemblyBasePaths = referenceAssemblyPaths.
                Select(Utilities.GetDirectoryPath).
                Distinct().
                ToArray();

            var assembleOptions = AssembleOptions.Deterministic;
            if (applyOptimization)
            {
                assembleOptions |= AssembleOptions.ApplyOptimization;
            }

            var assembler = new Assembler(
                logger,
                referenceAssemblyBasePaths);

            if (assembler.Assemble(
                outputAssemblyPath,
                new()
                {
                    ReferenceAssemblyPaths = referenceAssemblyPaths.ToArray(),
                    AssemblyType = assemblyType,
                    DebugSymbolType = debugSymbolType,
                    Options = assembleOptions,
                    Version = version,
                    TargetFrameworkMoniker = targetFrameworkMoniker,
                },
                sourcePaths))
            {
                logger.Trace($"Finished.");
                return 0;
            }
            else
            {
                logger.Trace($"Failed assembling.");
                return 2;
            }
        }
        catch (OptionException)
        {
            WriteUsage(options);
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Marshal.GetHRForException(ex);
        }
    }
}
