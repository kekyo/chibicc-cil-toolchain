/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using chibicc.toolchain.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace chibild.Cli;

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

    private readonly string defaultTargetFrameworkMoniker;

    public string OutputAssemblyPath = null!;
    public readonly LinkerOptions LinkerOptions = new();
    public string? InjectToAssemblyPath;
    public LogLevels LogLevel = LogLevels.Warning;
    public bool ShowHelp = false;
    public string BaseInputPath = Directory.GetCurrentDirectory();
    public InputReference[] InputReferences = CommonUtilities.Empty<InputReference>();

    private CliOptions(string defaultTargetFrameworkMoniker) =>
        this.defaultTargetFrameworkMoniker = defaultTargetFrameworkMoniker;

    public static CliOptions Parse(
        string[] args,
        string defaultTargetFrameworkMoniker)
    {
        var options = new CliOptions(defaultTargetFrameworkMoniker);
        
        var libraryBasePaths = new List<string>();
        var inputReferences = new List<InputReference>();
        var prependExecutionSearchPaths = new List<string>();

        options.LinkerOptions.CreationOptions!.TargetFramework =
            TargetFramework.TryParse(defaultTargetFrameworkMoniker, out var tf) ?
                tf : TargetFramework.Default;

        static bool TryGetOptionArgument(
            string[] args, ref int index, out string result)
        {
            if (args[index].Length >= 3)
            {
                result = args[index].Substring(2);
                return true;
            }
            else if (args.Length >= index)
            {
                result = args[++index];
                return true;
            }
            result = null!;
            return false;
        }
        
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
                            if (TryGetOptionArgument(args, ref index, out var path1))
                            {
                                options.OutputAssemblyPath = Path.GetFullPath(path1);
                                continue;
                            }
                            break;
                        case 'L':
                            if (TryGetOptionArgument(args, ref index, out var path2))
                            {
                                libraryBasePaths.Add(Path.GetFullPath(path2));
                                continue;
                            }
                            break;
                        case 'l':
                            if (TryGetOptionArgument(args, ref index, out var path3))
                            {
                                inputReferences.Add(new LibraryNameReference(path3));
                                continue;
                            }
                            break;
                        case 'j':
                            if (TryGetOptionArgument(args, ref index, out var path4))
                            {
                                options.InjectToAssemblyPath = path4;
                                options.LinkerOptions.CreationOptions = null;
                                continue;
                            }
                            options.LinkerOptions.CreationOptions = null;
                            continue;
                        case 'a':
                            if (TryGetOptionArgument(args, ref index, out var path5))
                            {
                                if (options.LinkerOptions.CreationOptions is { } co2)
                                {
                                    co2.AppHostTemplatePath = path5;
                                }
                                continue;
                            }
                            break;
                        case 'd':
                            if (TryGetOptionArgument(args, ref index, out var path6))
                            {
                                if (options.LinkerOptions.CreationOptions is { } co2)
                                {
                                    co2.CAbiStartUpObjectDirectoryPath = path6;
                                }
                                continue;
                            }
                            break;
                        case 'e':
                            if (TryGetOptionArgument(args, ref index, out var path7))
                            {
                                if (options.LinkerOptions.CreationOptions is { } co2)
                                {
                                    co2.EntryPointSymbol = path7;
                                }
                                continue;
                            }
                            break;
                        case 'B':
                            if (TryGetOptionArgument(args, ref index, out var path8))
                            {
                                prependExecutionSearchPaths.Add(path8);
                                continue;
                            }
                            break;
                        case 'g':
                            if (arg.Length == 3)
                            {
                                switch (arg[2])
                                {
                                    case '0':
                                        options.LinkerOptions.DebugSymbolType =
                                            DebugSymbolTypes.None;
                                        continue;
                                    case '1':
                                        options.LinkerOptions.DebugSymbolType =
                                            DebugSymbolTypes.Portable;
                                        continue;
                                    case '2':
                                        options.LinkerOptions.DebugSymbolType =
                                            DebugSymbolTypes.Embedded;
                                        continue;
                                    case 'm':
                                        options.LinkerOptions.DebugSymbolType =
                                            DebugSymbolTypes.Mono;
                                        continue;
                                    case 'w':
                                        options.LinkerOptions.DebugSymbolType =
                                            DebugSymbolTypes.WindowsProprietary;
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.LinkerOptions.DebugSymbolType = DebugSymbolTypes.Embedded;
                                continue;
                            }
                            break;
                        case 's':
                            if (arg == "-shared")
                            {
                                if (options.LinkerOptions.CreationOptions is { } co1)
                                {
                                    co1.AssemblyType = AssemblyTypes.Dll;
                                }
                                continue;
                            }
                            if (arg.Length == 2)
                            {
                                options.LinkerOptions.DebugSymbolType = DebugSymbolTypes.None;
                                continue;
                            }
                            break;
                        case 'O':
                            if (arg.Length == 3)
                            {
                                switch (arg[2])
                                {
                                    case '0':
                                        options.LinkerOptions.ApplyOptimization = false;
                                        if (options.LinkerOptions.CreationOptions is { } co2)
                                        {
                                            co2.AssemblyOptions |=
                                                AssemblyOptions.DisableJITOptimization;
                                        }
                                        continue;
                                    case '1':
                                        options.LinkerOptions.ApplyOptimization = true;
                                        if (options.LinkerOptions.CreationOptions is { } co3)
                                        {
                                            co3.AssemblyOptions &=
                                                ~AssemblyOptions.DisableJITOptimization;
                                        }
                                        continue;
                                }
                            }
                            else if (arg.Length == 2)
                            {
                                options.LinkerOptions.ApplyOptimization = true;
                                if (options.LinkerOptions.CreationOptions is { } co2)
                                {
                                    co2.AssemblyOptions &=
                                        ~AssemblyOptions.DisableJITOptimization;
                                }
                                continue;
                            }
                            break;
                        case 'v':
                            if (TryGetOptionArgument(args, ref index, out var vs))
                            {
                                if (Version.TryParse(vs, out var version))
                                {
                                    if (options.LinkerOptions.CreationOptions is { } co2)
                                    {
                                        co2.Version = version;
                                    }
                                }
                                continue;
                            }
                            break;
                        case 'm':
                            if (TryGetOptionArgument(args, ref index, out var mopt))
                            {
                                if (TargetFramework.TryParse(mopt, out var tf2))
                                {
                                    if (options.LinkerOptions.CreationOptions is { } co2)
                                    {
                                        co2.TargetFramework = tf2;
                                    }
                                    continue;
                                }
                                if (Enum.TryParse<TargetWindowsArchitectures>(mopt, true, out var arch))
                                {
                                    if (options.LinkerOptions.CreationOptions is { } co2)
                                    {
                                        co2.TargetWindowsArchitecture = arch;
                                    }
                                    continue;
                                }
                                if (rollforwards.TryGetValue(mopt, out var rollforward))
                                {
                                    if (options.LinkerOptions.CreationOptions is { } co2)
                                    {
                                        co2.RuntimeConfiguration = rollforward;
                                    }
                                    continue;
                                }
                                switch (mopt.ToLowerInvariant())
                                {
                                    case "dll":
                                        if (options.LinkerOptions.CreationOptions is { } co2)
                                        {
                                            co2.AssemblyType = AssemblyTypes.Dll;
                                        }
                                        continue;
                                    case "exe":
                                        if (options.LinkerOptions.CreationOptions is { } co3)
                                        {
                                            co3.AssemblyType = AssemblyTypes.Exe;
                                        }
                                        continue;
                                    case "winexe":
                                        if (options.LinkerOptions.CreationOptions is { } co4)
                                        {
                                            co4.AssemblyType = AssemblyTypes.WinExe;
                                        }
                                        continue;
                                }
                            }
                            break;
                        case 'x':
                            options.LinkerOptions.WillCopyRequiredAssemblies = false;
                            continue;
                        case 'h':
                            options.ShowHelp = true;
                            continue;
                        case '-':
                            switch (arg.Substring(2).ToLowerInvariant())
                            {
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
                                    options.LinkerOptions.IsDryRun = true;
                                    continue;
                                case "help":
                                    options.ShowHelp = true;
                                    continue;
                            }
                            break;
                    }

                    throw new InvalidOptionException($"Invalid option: {arg}");
                }

                if (arg == "-")
                {
                    inputReferences.Add(new ObjectFilePathReference("-"));
                }
                else if (Path.GetExtension(arg) is ".a" or ".so" or ".dylib" or ".dll")
                {
                    inputReferences.Add(new LibraryPathReference(arg));
                }
                else
                {
                    inputReferences.Add(new ObjectFilePathReference(arg));
                }
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
                switch (options.LinkerOptions.CreationOptions?.AssemblyType)
                {
                    case AssemblyTypes.Exe:
                    case AssemblyTypes.WinExe:
                        options.OutputAssemblyPath =
                            options.LinkerOptions.CreationOptions.TargetFramework.Identifier ==
                            TargetFrameworkIdentifiers.NETFramework
                                ? Path.GetFullPath("a.out.exe")
                                : Path.GetFullPath("a.out.dll");
                        break;
                    default:
                        switch (options.InputReferences.
                            FirstOrDefault(ir => ir is ObjectFilePathReference))
                        {
                            case null:
                            case ObjectFilePathReference("-"):
                                options.OutputAssemblyPath = Path.GetFullPath("a.out.dll");
                                break;
                            case ObjectFilePathReference(var path):
                                options.OutputAssemblyPath = Path.Combine(
                                    CommonUtilities.GetDirectoryPath(path),
                                    Path.GetFileNameWithoutExtension(path) + ".dll");
                                break;
                        }

                        break;
                }
                break;
            // HACK:
            case "/dev/null":
                options.LinkerOptions.IsDryRun = true;
                break;
        }

        options.LinkerOptions.LibraryReferenceBasePaths = libraryBasePaths.
            Distinct().
            ToArray();
        options.InputReferences = inputReferences.
            Distinct().
            ToArray();
        options.LinkerOptions.PrependExecutionSearchPaths = prependExecutionSearchPaths.
            Distinct().
            ToArray();

        return options;
    }

    public void WriteOptions(ILogger logger)
    {
        foreach (var inputFilePath in this.InputReferences)
        {
            logger.Information($"InputFilePaths={inputFilePath}");
        }

        logger.Information($"OutputAssemblyPath={this.OutputAssemblyPath}");

        foreach (var path in this.LinkerOptions.LibraryReferenceBasePaths)
        {
            logger.Information($"ReferenceAssemblyBasePath={path}");
        }

        foreach (var lr in this.InputReferences)
        {
            logger.Information($"InputReference={lr}");
        }

        logger.Information($"DebugSymbolType={this.LinkerOptions.DebugSymbolType}");
        logger.Information($"ApplyOptimization={this.LinkerOptions.ApplyOptimization}");
        logger.Information($"WillCopyRequiredAssemblies={this.LinkerOptions.WillCopyRequiredAssemblies}");

        if (this.LinkerOptions.CreationOptions is { } co)
        {
            logger.Information($"AssemblyOptions={co.AssemblyOptions}");
            logger.Information($"AssemblyType={co.AssemblyType}");
            logger.Information($"TargetWindowsArchitecture={co.TargetWindowsArchitecture}");
            logger.Information($"RuntimeConfiguration={co.RuntimeConfiguration}");
            logger.Information($"CAbiStartUpDirectoryPath={co.CAbiStartUpObjectDirectoryPath ?? "(null)"}");
            logger.Information($"EntryPointSymbol={co.EntryPointSymbol}");
            logger.Information($"Version={co.Version}");
            logger.Information($"TargetFramework={co.TargetFramework}");
            logger.Information($"AppHostTemplatePath={(co.AppHostTemplatePath ?? "(null)")}");
        }
        else
        {
            logger.Information($"InjectToAssemblyPath={this.InjectToAssemblyPath}");
        }
    
        foreach (var sp in this.LinkerOptions.PrependExecutionSearchPaths)
        {
            logger.Information($"PrependExecutionSearchPath={sp}");
        }

        logger.Information($"IsDryRun={this.LinkerOptions.IsDryRun}");
    }

    public void WriteUsage(TextWriter tw)
    {
        tw.WriteLine("  -o <path>         Output assembly path");
        tw.WriteLine("  -shared, -mdll    Produce dll assembly");
        tw.WriteLine("           -mexe    Produce executable assembly (defaulted)");
        tw.WriteLine("           -mwinexe Produce Windows executable assembly");
        tw.WriteLine("  -L <path>         Reference assembly base path");
        tw.WriteLine("  -l <name>         Reference assembly name");
        tw.WriteLine("  -j <path>         Will inject into an assembly file");
        tw.WriteLine("  -g, -g2           Produce embedded debug symbol (defaulted)");
        tw.WriteLine("      -g1           Produce portable debug symbol file");
        tw.WriteLine("      -gm           Produce mono debug symbol file");
        tw.WriteLine("      -gw           Produce windows proprietary debug symbol file");
        tw.WriteLine("  -s, -g0           Omit debug symbol file");
        tw.WriteLine("  -O, -O1           Apply optimization");
        tw.WriteLine("      -O0           Disable optimization (defaulted)");
        tw.WriteLine("  -x                Will not copy required assemblies");
        tw.WriteLine("  -d <path>         CABI startup object directory path");
        tw.WriteLine("  -e <symbol>       Entry point symbol (defaulted: _start)");
        tw.WriteLine("  -B <path>         Prepend execution search path");
        tw.WriteLine("  -v <version>      Apply assembly version (defaulted: 1.0.0.0)");
        tw.WriteLine($"  -m <tfm>          Target framework moniker (defaulted: {defaultTargetFrameworkMoniker})");
        tw.WriteLine("  -m <arch>         Target Windows architecture [AnyCPU|Preferred32Bit|X86|X64|IA64|ARM|ARMv7|ARM64] (defaulted: AnyCPU)");
        tw.WriteLine("  -m <rollforward>  CoreCLR rollforward configuration [Major|Minor|Feature|Patch|LatestMajor|LatestMinor|LatestFeature|LatestPatch|Disable|Default|Omit] (defaulted: Major)");
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
