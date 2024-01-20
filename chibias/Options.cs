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
            ThisAssembly.AssemblyMetadata.TargetFrameworkMoniker;

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
                            if (arg.Length == 3)
                            {
                                switch (arg[2])
                                {
                                    case '0':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRMajorRollForward;
                                        continue;
                                    case '1':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRMinorRollForward;
                                        continue;
                                    case '2':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRFeatureRollForward;
                                        continue;
                                    case '3':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRPatchRollForward;
                                        continue;
                                    case '4':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRLatestMajorRollForward;
                                        continue;
                                    case '5':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRLatestMinorRollForward;
                                        continue;
                                    case '6':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRLatestFeatureRollForward;
                                        continue;
                                    case '7':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRLatestPatchRollForward;
                                        continue;
                                    case '8':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLRDisableRollForward;
                                        continue;
                                    case 'n':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.ProduceCoreCLR;
                                        continue;
                                    case 'o':
                                        options.AssemblerOptions.RuntimeConfiguration =
                                            RuntimeConfigurationOptions.Omit;
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.AssemblerOptions.RuntimeConfiguration =
                                    RuntimeConfigurationOptions.ProduceCoreCLRMajorRollForward;
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
                    options.OutputAssemblyPath = Path.GetFullPath("a.out.exe");
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
        logger.Information($"TargetFrameworkMoniker={this.AssemblerOptions.TargetFrameworkMoniker}");
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
        tw.WriteLine("  -r <path>         Reference assembly path");
        tw.WriteLine("  -g, -g2           Produce embedded debug symbol (defaulted)");
        tw.WriteLine("      -g1           Produce portable debug symbol file");
        tw.WriteLine("      -gm           Produce mono debug symbol file");
        tw.WriteLine("      -gw           Produce windows proprietary debug symbol file");
        tw.WriteLine("      -g0           Omit debug symbol file");
        tw.WriteLine("  -O, -O1           Apply optimization");
        tw.WriteLine("      -O0           Disable optimization (defaulted)");
        tw.WriteLine("  -p, -p0           Produce CoreCLR runtime configuration (rollForward: major) (defaulted)");
        tw.WriteLine("      -p1           Produce CoreCLR runtime configuration (rollForward: minor)");
        tw.WriteLine("      -p2           Produce CoreCLR runtime configuration (rollForward: feature)");
        tw.WriteLine("      -p3           Produce CoreCLR runtime configuration (rollForward: patch)");
        tw.WriteLine("      -p4           Produce CoreCLR runtime configuration (rollForward: latest major)");
        tw.WriteLine("      -p5           Produce CoreCLR runtime configuration (rollForward: latest minor)");
        tw.WriteLine("      -p6           Produce CoreCLR runtime configuration (rollForward: latest feature)");
        tw.WriteLine("      -p7           Produce CoreCLR runtime configuration (rollForward: latest patch)");
        tw.WriteLine("      -p8           Produce CoreCLR runtime configuration (rollForward: disable)");
        tw.WriteLine("      -pn           Produce CoreCLR runtime configuration");
        tw.WriteLine("      -po           Omit CoreCLR runtime configuration");
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
