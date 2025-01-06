/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Logging;
using System;
using System.IO;

namespace chibias.Cli;

public sealed class CliOptions
{
    public string OutputObjectFilePath = null!;
    public bool IsLinked = true;
    public bool IsDryRun = false;
#if DEBUG
    public LogLevels LogLevel = LogLevels.Trace;
#else
    public LogLevels LogLevel = LogLevels.Warning;
#endif
    public bool ShowHelp = false;
    public string? SourceFilePath;

    private CliOptions()
    {
    }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

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
                                var outputObjectFilePath =
                                    Path.GetFullPath(arg.Substring(2));
                                options.OutputObjectFilePath = outputObjectFilePath;
                                continue;
                            }
                            else if (args.Length >= index)
                            {
                                var outputObjectFilePath =
                                    Path.GetFullPath(args[++index]);
                                options.OutputObjectFilePath = outputObjectFilePath;
                                continue;
                            }
                            break;
                        case 'c':
                            options.IsLinked = false;
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
                                    options.IsDryRun = true;
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
                options.SourceFilePath = sourceCodePath;
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

        switch (options.OutputObjectFilePath)
        {
            case null:
                options.OutputObjectFilePath = Path.GetFullPath("a.out");
                break;
            // HACK:
            case "/dev/null":
                options.IsDryRun = true;
                break;
        }

        return options;
    }

    public static void WriteUsage(TextWriter tw)
    {
        tw.WriteLine("  -o <path>         Output object file path");
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
