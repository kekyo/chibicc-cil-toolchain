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
    private static readonly Dictionary<string, RuntimeConfigurationOptions> rollforwards = new()
    {
        { "major", RuntimeConfigurationOptions.ProduceCoreCLRMajorRollForward },
        { "minor", RuntimeConfigurationOptions.ProduceCoreCLRMinorRollForward },
        { "feature", RuntimeConfigurationOptions.ProduceCoreCLRFeatureRollForward },
        { "patch", RuntimeConfigurationOptions.ProduceCoreCLRPatchRollForward },
        { "latestmajor", RuntimeConfigurationOptions.ProduceCoreCLRLatestMajorRollForward },
        { "latestminor", RuntimeConfigurationOptions.ProduceCoreCLRLatestMinorRollForward },
        { "latestfeature", RuntimeConfigurationOptions.ProduceCoreCLRLatestFeatureRollForward },
        { "latestpatch", RuntimeConfigurationOptions.ProduceCoreCLRLatestPatchRollForward },
        { "disable", RuntimeConfigurationOptions.ProduceCoreCLRDisableRollForward },
        { "default", RuntimeConfigurationOptions.ProduceCoreCLR },
        { "omit", RuntimeConfigurationOptions.Omit },
    };

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

        options.AssemblerOptions.TargetFramework =
            TargetFramework.TryParse(ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker, out var tf) ?
                tf : TargetFramework.Default;

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
                        case 'a':
                            if (args.Length >= index)
                            {
                                options.AssemblerOptions.AppHostTemplatePath = args[index + 1];
                                index++;
                                continue;
                            }
                            break;
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
                                    case 'm':
                                        options.AssemblerOptions.DebugSymbolType =
                                            DebugSymbolTypes.Mono;
                                        continue;
                                    case 'w':
                                        options.AssemblerOptions.DebugSymbolType =
                                            DebugSymbolTypes.WindowsProprietary;
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
                                        options.AssemblerOptions.Options |=
                                            AssembleOptions.DisableJITOptimization;
                                        continue;
                                    case '1':
                                        options.AssemblerOptions.Options |=
                                            AssembleOptions.ApplyOptimization;
                                        options.AssemblerOptions.Options &=
                                            ~AssembleOptions.DisableJITOptimization;
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.AssemblerOptions.Options |=
                                    AssembleOptions.ApplyOptimization;
                                options.AssemblerOptions.Options &=
                                    ~AssembleOptions.DisableJITOptimization;
                                continue;
                            }
                            break;
                        case 'p':
                            if (args.Length >= index &&
                                rollforwards.TryGetValue(args[index + 1], out var rollforward))
                            {
                                index++;
                                options.AssemblerOptions.RuntimeConfiguration = rollforward;
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
                            if (arg.Length >= 2 &&
                                TargetFramework.TryParse(args[index + 1], out var tf2))
                            {
                                index++;
                                options.AssemblerOptions.TargetFramework = tf2;
                                continue;
                            }
                            break;
                        case 'w':
                            if (args.Length >= index &&
                                Enum.TryParse<TargetWindowsArchitectures>(args[index + 1], true, out var arch))
                            {
                                index++;
                                options.AssemblerOptions.TargetWindowsArchitecture = arch;
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
                                    if (args.Length >= index &&
                                        Enum.TryParse<LogLevels>(args[index + 1], true, out var logLevel))
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
            switch (options.AssemblerOptions.AssemblyType)
            {
                case AssemblyTypes.Exe:
                case AssemblyTypes.WinExe:
                    options.OutputAssemblyPath = options.AssemblerOptions.TargetFramework.Identifier == TargetFrameworkIdentifiers.NETFramework ?
                        Path.GetFullPath("a.out.exe") : Path.GetFullPath("a.out.dll");
                    break;
                default:
                    switch (options.SourceCodePaths.FirstOrDefault())
                    {
                        case null:
                        case "-":
                            options.OutputAssemblyPath = Path.GetFullPath("a.out.dll");
                            break;
                        case { } path:
                            options.OutputAssemblyPath = Path.Combine(
                                Utilities.GetDirectoryPath(path),
                                Path.GetFileNameWithoutExtension(path) + ".dll");
                            break;
                    }
                    break;
            }
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
        logger.Information($"TargetWindowsArchitecture={this.AssemblerOptions.TargetWindowsArchitecture}");
        logger.Information($"DebugSymbolType={this.AssemblerOptions.DebugSymbolType}");
        logger.Information($"Options={this.AssemblerOptions.Options}");
        logger.Information($"RuntimeConfiguration={this.AssemblerOptions.RuntimeConfiguration}");
        logger.Information($"Version={this.AssemblerOptions.Version}");
        logger.Information($"TargetFrameworkMoniker={this.AssemblerOptions.TargetFramework}");
        logger.Information($"AppHostTemplatePath={(this.AssemblerOptions.AppHostTemplatePath ?? "(null)")}");
    }

    public static void WriteUsage(TextWriter tw)
    {
        tw.WriteLine($"chibias [{ThisAssembly.AssemblyVersion},{ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker}] [{ThisAssembly.AssemblyMetadata.CommitId}]");
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
        tw.WriteLine("  -a <path>         AppHost template path");
        tw.WriteLine("  -r <path>         Reference assembly path");
        tw.WriteLine("  -g, -g2           Produce embedded debug symbol (defaulted)");
        tw.WriteLine("      -g1           Produce portable debug symbol file");
        tw.WriteLine("      -gm           Produce mono debug symbol file");
        tw.WriteLine("      -gw           Produce windows proprietary debug symbol file");
        tw.WriteLine("      -g0           Omit debug symbol file");
        tw.WriteLine("  -O, -O1           Apply optimization");
        tw.WriteLine("      -O0           Disable optimization (defaulted)");
        tw.WriteLine("  -p <rollforward>  CoreCLR rollforward configuration [Major|Minor|Feature|Patch|LatestMajor|LatestMinor|LatestFeature|LatestPatch|Disable|Default|Omit]");
        tw.WriteLine("  -v <version>      Apply assembly version (defaulted: 1.0.0.0)");
        tw.WriteLine($"  -f <tfm>          Target framework moniker (defaulted: {ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker})");
        tw.WriteLine("  -w <arch>         Target Windows architecture [AnyCPU|Preferred32Bit|X86|X64|IA64|ARM|ARMv7|ARM64]");
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
