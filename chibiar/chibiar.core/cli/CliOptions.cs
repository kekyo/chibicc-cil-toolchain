/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using chibicc.toolchain.Logging;

namespace chibiar.cli;

public enum ArchiveModes
{
    Nothing,
    Add,
    Delete,
    List,
}

public sealed class CliOptions
{
    public string ArchiveFilePath = null!;
    public ArchiveModes Mode = ArchiveModes.Nothing;
    public bool IsSilent = false;
    public SymbolTableModes SymbolTableMode = SymbolTableModes.Auto;
    public bool IsDryRun = false;
    public LogLevels LogLevel = LogLevels.Warning;
    public bool ShowHelp = false;
    public readonly List<string> ObjectFilePaths = new();

    private CliOptions()
    {
    }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        if (args.Length == 0)
        {
            return options;
        }

        var arg0 = args[0];
        if (arg0.StartsWith("-"))
        {
            arg0 = arg0.Substring(1);
        }

        for (var index = 0; index < arg0.Length; index++)
        {
            try
            {
                switch (arg0[index])
                {
                    case 'r':
                        options.Mode = ArchiveModes.Add;
                        break;
                    case 'd':
                        options.Mode = ArchiveModes.Delete;
                        break;
                    case 't':
                        options.Mode = ArchiveModes.List;
                        break;
                    case 'c':
                        options.IsSilent = true;
                        break;
                    case 's':
                        options.SymbolTableMode = SymbolTableModes.ForceUpdate;
                        break;
                    case 'S':
                        options.SymbolTableMode = SymbolTableModes.ForceIgnore;
                        break;
                    case 'h':
                        options.ShowHelp = true;
                        continue;
                    case '-':
                        switch (arg0.Substring(2).ToLowerInvariant())
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
                                options.IsDryRun = true;
                                continue;
                            case "help":
                                options.ShowHelp = true;
                                continue;
                        }
                        break;
                    default:
                        throw new InvalidOptionException($"Invalid option: {arg0}");
                }
            }
            catch (InvalidOptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOptionException($"Invalid option: {arg0}, {ex.Message}");
            }
        }
        
        switch (args[1])
        {
            case "-":
                options.ArchiveFilePath = "-";
                break;
            // HACK:
            case "/dev/null":
                options.ArchiveFilePath = args[1];
                options.IsDryRun = true;
                break;
            default:
                options.ArchiveFilePath = Path.GetFullPath(args[1]);
                break;
        }

        for (var index = 2; index < args.Length; index++)
        {
            options.ObjectFilePaths.Add(Path.GetFullPath(args[index]));
        }

        return options;
    }

    public static void WriteUsage(TextWriter tw)
    {
        tw.WriteLine("  -r                Add object files into the archive");
        tw.WriteLine("  -c                Add object files into the archive silently");
        tw.WriteLine("  -s                Add symbol table");
        tw.WriteLine("  -d                Delete object files from the archive");
        tw.WriteLine("  -t                List object files in the archive");
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
