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

namespace chibiar.Cli;

public enum ArchiveModes
{
    Nothing,
    AddOrUpdate,
    Extract,
    Delete,
    List,
}

public sealed class CliOptions
{
    public string ArchiveFilePath = null!;
    public ArchiveModes Mode = ArchiveModes.Nothing;
    public bool IsSilent = false;
    public bool IsCreateSymbolTable = true;
    public bool IsDryRun = false;
#if DEBUG
    public LogLevels LogLevel = LogLevels.Trace;
#else
    public LogLevels LogLevel = LogLevels.Warning;
#endif
    public bool ShowHelp = false;
    public readonly List<string> ObjectNames = new();

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
                    case 'u':
                        options.Mode = ArchiveModes.AddOrUpdate;
                        break;
                    case 'x':
                        options.Mode = ArchiveModes.Extract;
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
                        options.IsCreateSymbolTable = true;
                        break;
                    case 'S':
                        options.IsCreateSymbolTable = false;
                        break;
                    case 'h':
                        options.ShowHelp = true;
                        continue;
                    case '-':
                        switch (arg0.Substring(1).ToLowerInvariant())
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
            options.ObjectNames.Add(args[index]);
        }

        return options;
    }

    public static void WriteUsage(TextWriter tw)
    {
        tw.WriteLine("  -r, -u            Add or update object files into the archive");
        tw.WriteLine("  -c                Create archive file silently");
        tw.WriteLine("  -x                Extract object files from the archive");
        tw.WriteLine("  -d                Delete object files from the archive");
        tw.WriteLine("  -s                Add symbol table (default)");
        tw.WriteLine("  -S                Will not add symbol table");
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
