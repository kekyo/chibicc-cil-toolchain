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

namespace chibias.cli;

public sealed class CliOptions
{
    private static readonly Dictionary<string, RuntimeConfigurationOptions> rollforwards = new(StringComparer.OrdinalIgnoreCase)
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
    public readonly AssemblerOptions AssemblerOptions = new();
    public LogLevels LogLevel = LogLevels.Warning;
    public bool ShowHelp = false;
    public readonly List<string> SourceCodePaths = new();

    private CliOptions()
    {
    }

    public static CliOptions Parse(string[] args, string defaultTargetFrameworkMoniker)
    {
        var options = new CliOptions();
        var referenceAssemblyBasePaths = new List<string>();
        var referenceAssemblyNames = new List<string>();

        options.AssemblerOptions.CreationOptions!.TargetFramework =
            TargetFramework.TryParse(defaultTargetFrameworkMoniker, out var tf) ?
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
                        case 'L':
                            if (arg.Length >= 3)
                            {
                                var referenceAssemblyBasePath =
                                    Path.GetFullPath(arg.Substring(2));
                                referenceAssemblyBasePaths.Add(referenceAssemblyBasePath);
                                continue;
                            }
                            else if (args.Length >= index)
                            {
                                var referenceAssemblyBasePath =
                                    Path.GetFullPath(args[++index]);
                                referenceAssemblyBasePaths.Add(referenceAssemblyBasePath);
                                continue;
                            }
                            break;
                        case 'l':
                            if (arg.Length >= 3)
                            {
                                var referenceAssemblyName = arg.Substring(2);
                                referenceAssemblyNames.Add(referenceAssemblyName);
                                continue;
                            }
                            else if (args.Length >= index)
                            {
                                var referenceAssemblyName = args[++index];
                                referenceAssemblyNames.Add(referenceAssemblyName);
                                continue;
                            }
                            break;
                        case 'c':
                            if (options.AssemblerOptions.CreationOptions is { } co1)
                            {
                                co1.AssemblyType = AssemblyTypes.Dll;
                            }
                            continue;
                        case 'i':
                            options.AssemblerOptions.CreationOptions = null;
                            continue;
                        case 'a':
                            if (args.Length >= index)
                            {
                                if (options.AssemblerOptions.CreationOptions is { } co2)
                                {
                                    co2.AppHostTemplatePath = args[index + 1];
                                }
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
                                options.AssemblerOptions.DebugSymbolType = DebugSymbolTypes.Embedded;
                                continue;
                            }
                            break;
                        case 'O':
                            if (arg.Length == 3)
                            {
                                switch (arg[2])
                                {
                                    case '0':
                                        options.AssemblerOptions.ApplyOptimization = false;
                                        if (options.AssemblerOptions.CreationOptions is { } co2)
                                        {
                                            co2.Options |=
                                                AssembleOptions.DisableJITOptimization;
                                        }
                                        continue;
                                    case '1':
                                        options.AssemblerOptions.ApplyOptimization = true;
                                        if (options.AssemblerOptions.CreationOptions is { } co3)
                                        {
                                            co3.Options &=
                                                ~AssembleOptions.DisableJITOptimization;
                                        }
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.AssemblerOptions.ApplyOptimization = true;
                                if (options.AssemblerOptions.CreationOptions is { } co4)
                                {
                                    co4.Options &=
                                        ~AssembleOptions.DisableJITOptimization;
                                }
                                continue;
                            }
                            break;
                        case 'p':
                            if (args.Length >= index &&
                                rollforwards.TryGetValue(args[index + 1], out var rollforward))
                            {
                                index++;
                                if (options.AssemblerOptions.CreationOptions is { } co5)
                                {
                                    co5.RuntimeConfiguration = rollforward;
                                }
                                continue;
                            }
                            break;
                        case 'v':
                            if (arg.Length >= 2 &&
                                Version.TryParse(args[index + 1], out var version))
                            {
                                index++;
                                if (options.AssemblerOptions.CreationOptions is { } co6)
                                {
                                    co6.Version = version;
                                }
                                continue;
                            }
                            break;
                        case 'f':
                            if (arg.Length >= 2 &&
                                TargetFramework.TryParse(args[index + 1], out var tf2))
                            {
                                index++;
                                if (options.AssemblerOptions.CreationOptions is { } co7)
                                {
                                    co7.TargetFramework = tf2;
                                }
                                continue;
                            }
                            break;
                        case 'w':
                            if (args.Length >= index &&
                                Enum.TryParse<TargetWindowsArchitectures>(args[index + 1], true, out var arch))
                            {
                                index++;
                                if (options.AssemblerOptions.CreationOptions is { } co8)
                                {
                                    co8.TargetWindowsArchitecture = arch;
                                }
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
                                    if (options.AssemblerOptions.CreationOptions is { } co9)
                                    {
                                        co9.AssemblyType = AssemblyTypes.Dll;
                                    }
                                    continue;
                                case "exe":
                                    if (options.AssemblerOptions.CreationOptions is { } co10)
                                    {
                                        co10.AssemblyType = AssemblyTypes.Exe;
                                    }
                                    continue;
                                case "winexe":
                                    if (options.AssemblerOptions.CreationOptions is { } co11)
                                    {
                                        co11.AssemblyType = AssemblyTypes.WinExe;
                                    }
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
                                case "dryrun":
                                    options.AssemblerOptions.IsDryRun = true;
                                    continue;
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

        switch (options.OutputAssemblyPath)
        {
            case null:
                switch (options.AssemblerOptions.CreationOptions?.AssemblyType)
                {
                    case AssemblyTypes.Exe:
                    case AssemblyTypes.WinExe:
                        options.OutputAssemblyPath =
                            options.AssemblerOptions.CreationOptions.TargetFramework.Identifier ==
                            TargetFrameworkIdentifiers.NETFramework
                                ? Path.GetFullPath("a.out.exe")
                                : Path.GetFullPath("a.out.dll");
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
                break;
            // HACK:
            case "/dev/null":
                options.AssemblerOptions.IsDryRun = true;
                break;
        }

        options.AssemblerOptions.ReferenceAssemblyBasePaths = referenceAssemblyBasePaths.
            Distinct().
            ToArray();
        options.AssemblerOptions.ReferenceAssemblyNames = referenceAssemblyNames.
            Distinct().
            ToArray();

        return options;
    }

    public void Write(ILogger logger)
    {
        foreach (var path in this.SourceCodePaths)
        {
            logger.Information($"SourceCode={path}");
        }

        logger.Information($"OutputAssemblyPath={this.OutputAssemblyPath}");

        foreach (var path in this.AssemblerOptions.ReferenceAssemblyBasePaths)
        {
            logger.Information($"ReferenceAssemblyBasePath={path}");
        }

        foreach (var name in this.AssemblerOptions.ReferenceAssemblyNames)
        {
            logger.Information($"ReferenceAssemblyName={name}");
        }

        logger.Information($"DebugSymbolType={this.AssemblerOptions.DebugSymbolType}");
        logger.Information($"ApplyOptimization={this.AssemblerOptions.ApplyOptimization}");

        if (this.AssemblerOptions.CreationOptions is { } co)
        {
            logger.Information($"Options={co.Options}");
            logger.Information($"AssemblyType={co.AssemblyType}");
            logger.Information($"TargetWindowsArchitecture={co.TargetWindowsArchitecture}");
            logger.Information($"RuntimeConfiguration={co.RuntimeConfiguration}");
            logger.Information($"Version={co.Version}");
            logger.Information($"TargetFrameworkMoniker={co.TargetFramework}");
            logger.Information($"AppHostTemplatePath={(co.AppHostTemplatePath ?? "(null)")}");
        }
        else
        {
            logger.Information($"WillInjectTo={this.OutputAssemblyPath}");
        }
        
        logger.Information($"IsDryRun={this.AssemblerOptions.IsDryRun}");
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
        tw.WriteLine("  -L <path>         Reference assembly base path");
        tw.WriteLine("  -l <name>         Reference assembly name");
        tw.WriteLine("  -i                Will inject to output assembly file");
        tw.WriteLine("  -g, -g2           Produce embedded debug symbol (defaulted)");
        tw.WriteLine("      -g1           Produce portable debug symbol file");
        tw.WriteLine("      -gm           Produce mono debug symbol file");
        tw.WriteLine("      -gw           Produce windows proprietary debug symbol file");
        tw.WriteLine("      -g0           Omit debug symbol file");
        tw.WriteLine("  -O, -O1           Apply optimization");
        tw.WriteLine("      -O0           Disable optimization (defaulted)");
        tw.WriteLine("  -v <version>      Apply assembly version (defaulted: 1.0.0.0)");
        tw.WriteLine($"  -f <tfm>          Target framework moniker (defaulted: {ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker})");
        tw.WriteLine("  -w <arch>         Target Windows architecture [AnyCPU|Preferred32Bit|X86|X64|IA64|ARM|ARMv7|ARM64] (defaulted: AnyCPU)");
        tw.WriteLine("  -p <rollforward>  CoreCLR rollforward configuration [Major|Minor|Feature|Patch|LatestMajor|LatestMinor|LatestFeature|LatestPatch|Disable|Default|Omit] (defaulted: Major)");
        tw.WriteLine("  -a <path>         .NET Core AppHost template path");
        tw.WriteLine("      --log <level> Log level [debug|trace|information|warning|error|silent] (defaulted: warning)");
        tw.WriteLine("      --dryrun      Need to dryrun");
        tw.WriteLine("  -h, --help        Show this help");
    }
}

public sealed class InvalidOptionException : Exception
{
    public InvalidOptionException(string message) :
        base(message)
    {
    }
}
