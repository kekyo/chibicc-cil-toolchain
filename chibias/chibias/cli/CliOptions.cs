/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace chibias.cli;

public sealed class CliOptions
{
    public string OutputObjectFilePath = null!;
    public bool IsLinked = true;
    public bool IsDryRun = false;
    public bool ShowHelp = false;
    public readonly List<string> SourceFilePaths = new();

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
                options.SourceFilePaths.Add(sourceCodePath);
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
