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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace chibias;

internal sealed class Options
{
    public string OutputAssemblyPath = null!;
    public readonly List<string> ReferenceAssemblyBasePaths = new();
    public readonly AssemblerOptions AssemblerOptions = new();
    public LogLevels LogLevel = LogLevels.Warning;
    public bool ShowHelp = false;
    public readonly List<string> SourceCodePaths = new();

    private Options()
    {
    }

    public static Options Parse(string[] args)
    {
        var options = new Options();
        var referenceAssemblyPaths = new List<string>();

        options.AssemblerOptions.TargetFrameworkMoniker =
            ThisAssembly.AssemblyMetadata.TargetFramework;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            try
            {
                if (arg.StartsWith("-") && arg.Length >= 2)
                {
                    switch (arg[1])
                    {
                        case 'o':
                            if (arg.Length >= 3)
                            {
                                var outputAssemblyPath =
                                    Path.GetFullPath(arg.Substring(2));
                                options.OutputAssemblyPath = outputAssemblyPath;
                                continue;
                            }
                            else if (args.Length >= index)
                            {
                                var outputAssemblyPath =
                                    Path.GetFullPath(args[++index]);
                                options.OutputAssemblyPath = outputAssemblyPath;
                                continue;
                            }
                            break;
                        case 'r':
                            if (arg.Length >= 3)
                            {
                                var referenceAssemblyPath =
                                    Path.GetFullPath(arg.Substring(2));
                                referenceAssemblyPaths.Add(referenceAssemblyPath);
                                continue;
                            }
                            else if (args.Length >= index)
                            {
                                var referenceAssemblyPath =
                                    Path.GetFullPath(args[++index]);
                                referenceAssemblyPaths.Add(referenceAssemblyPath);
                                continue;
                            }
                            break;
                        case 'c':
                            options.AssemblerOptions.AssemblyType = AssemblyTypes.Dll;
                            continue;
                        case 'g':
                            if (arg.Length == 3)
                            {
                                switch (arg[2])
                                {
                                    case '0':
                                        options.AssemblerOptions.DebugSymbolType =
                                            DebugSymbolTypes.None;
                                        continue;
                                    case '1':
                                        options.AssemblerOptions.DebugSymbolType =
                                            DebugSymbolTypes.Portable;
                                        continue;
                                    case '2':
                                        options.AssemblerOptions.DebugSymbolType =
                                            DebugSymbolTypes.Embedded;
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.AssemblerOptions.DebugSymbolType =
                                    DebugSymbolTypes.Embedded;
                                continue;
                            }
                            break;
                        case 'O':
                            if (arg.Length == 3)
                            {
                                switch (arg[2])
                                {
                                    case '0':
                                        options.AssemblerOptions.Options &=
                                            ~AssembleOptions.ApplyOptimization;
                                        continue;
                                    case '1':
                                        options.AssemblerOptions.Options |=
                                            AssembleOptions.ApplyOptimization;
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.AssemblerOptions.Options |=
                                    AssembleOptions.ApplyOptimization;
                                continue;
                            }
                            break;
                        case 'v':
                            if (arg.Length >= 2 &&
                                Version.TryParse(args[index + 1], out var version))
                            {
                                index++;
                                options.AssemblerOptions.Version = version;
                                continue;
                            }
                            break;
                        case 'f':
                            if (arg.Length >= 2)
                            {
                                options.AssemblerOptions.TargetFrameworkMoniker = args[++index];
                                continue;
                            }
                            break;
                        case 'h':
                            options.ShowHelp = true;
                            continue;
                        case '-':
                            switch (arg.Substring(2).ToLowerInvariant())
                            {
                                case "dll":
                                    options.AssemblerOptions.AssemblyType = AssemblyTypes.Dll;
                                    continue;
                                case "exe":
                                    options.AssemblerOptions.AssemblyType = AssemblyTypes.Exe;
                                    continue;
                                case "winexe":
                                    options.AssemblerOptions.AssemblyType = AssemblyTypes.WinExe;
                                    continue;
                                case "log":
                                    if (arg.Length >= 3 &&
                                        Enum.TryParse<LogLevels>(arg.Substring(2), out var logLevel))
                                    {
                                        options.LogLevel = logLevel;
                                        continue;
                                    }
                                    else if (args.Length >= index &&
                                        Enum.TryParse<LogLevels>(args[index + 1], true, out logLevel))
                                    {
                                        index++;
                                        options.LogLevel = logLevel;
                                        continue;
                                    }
                                    break;
                                case "help":
                                    options.ShowHelp = true;
                                    continue;
                            }
                            break;
                    }

                    throw new InvalidOptionException($"Invalid option: {arg}");
                }

                var sourceCodePath =
                    arg != "-" ? Path.GetFullPath(arg) : arg;
                options.SourceCodePaths.Add(sourceCodePath);
            }
            catch (InvalidOptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOptionException($"Invalid option: {arg}, {ex.Message}");
            }
        }

        if (options.OutputAssemblyPath == null)
        {
            options.OutputAssemblyPath = Path.GetFullPath(
                options.AssemblerOptions.AssemblyType == AssemblyTypes.Dll ?
                    "a.out.dll" : "a.out.exe");
        }

        options.AssemblerOptions.ReferenceAssemblyPaths =
            referenceAssemblyPaths.ToArray();

        options.ReferenceAssemblyBasePaths.AddRange(
            options.AssemblerOptions.ReferenceAssemblyPaths.
                Select(Utilities.GetDirectoryPath).
                Distinct());

        return options;
    }

    public void Write(ILogger logger)
    {
        foreach (var path in this.SourceCodePaths)
        {
            logger.Information($"SourceCode={path}");
        }

        logger.Information($"OutputAssemblyPath={this.OutputAssemblyPath}");

        foreach (var path in this.AssemblerOptions.ReferenceAssemblyPaths)
        {
            logger.Information($"ReferenceAssemblyPath={path}");
        }

        foreach (var path in this.ReferenceAssemblyBasePaths)
        {
            logger.Information($"ReferenceAssemblyBasePath={path}");
        }

        logger.Information($"AssemblyType={this.AssemblerOptions.AssemblyType}");
        logger.Information($"DebugSymbolType={this.AssemblerOptions.DebugSymbolType}");
        logger.Information($"Options={this.AssemblerOptions.Options}");
        logger.Information($"Version={this.AssemblerOptions.Version}");
        logger.Information($"TargetFrameworkMoniker={this.AssemblerOptions.TargetFrameworkMoniker}");
    }

    public static void WriteUsage(TextWriter tw)
    {
        tw.WriteLine($"chibias [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFramework}] [{ThisAssembly.AssemblyMetadata.CommitId}]");
        tw.WriteLine("This is the CIL assembler, part of chibicc-cil project.");
        tw.WriteLine("https://github.com/kekyo/chibias-cil");
        tw.WriteLine("Copyright (c) Kouji Matsui");
        tw.WriteLine("License under MIT");
        tw.WriteLine();
        tw.WriteLine("usage: chibias [options] <source path> [<source path> ...]");
        tw.WriteLine("  -o <path>         Output assembly path");
        tw.WriteLine("  -c, --dll         Produce dll assembly");
        tw.WriteLine("      --exe         Produce executable assembly (defaulted)");
        tw.WriteLine("      --winexe      Produce Windows executable assembly");
        tw.WriteLine("  -r                Reference assembly path");
        tw.WriteLine("  -g, -g2           Produce embedded debug symbol (defaulted)");
        tw.WriteLine("      -g1           Produce portable debug symbol file");
        tw.WriteLine("      -g0           Omit debug symbol file");
        tw.WriteLine("  -O, -O1           Apply optimization");
        tw.WriteLine("      -O0           Disable optimization (defaulted)");
        tw.WriteLine("  -v <version>      Apply assembly version");
        tw.WriteLine($"  -f <tfm>          Target framework moniker (defaulted: {ThisAssembly.AssemblyMetadata.TargetFramework})");
        tw.WriteLine("      --log <level> Log level [debug|trace|information|warning|error|silent]");
        tw.WriteLine("  -h, --help        Show this help");
    }
}

internal sealed class InvalidOptionException : Exception
{
    public InvalidOptionException(string message) :
        base(message)
    {
    }
}
