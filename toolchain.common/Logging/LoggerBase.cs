/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Threading;

namespace chibicc.toolchain.Logging;

public abstract class LoggerBase : ILogger
{
    private static readonly int processId = Process.GetCurrentProcess().Id;
    private static int sequenceId;

    private readonly string prefix;

    public readonly LogLevels BaseLevel;

    protected LoggerBase(string prefix, LogLevels baseLevel)
    {
        this.BaseLevel = baseLevel;
        var id = Interlocked.Increment(ref sequenceId);
        this.prefix = $"{prefix} [{processId}{(id <= 1 ? string.Empty : $",{id}")}]:";
    }

    public void OutputLog(
        LogLevels logLevel, string? message, Exception? ex)
    {
        if (logLevel >= this.BaseLevel)
        {
            this.OnOutputLog(logLevel, message, ex);
        }
    }

    private static string GetLogLevelString(LogLevels logLevel) =>
        logLevel != LogLevels.Information ?
            $" {logLevel.ToString().ToLowerInvariant()}:" :
            string.Empty;

    protected virtual string? ToString(
        LogLevels logLevel, string? message, Exception? ex)
    {
        if (message is { } && ex is { })
        {
            return $"{prefix}{GetLogLevelString(logLevel)} {message}, {ex}";
        }
        else if (message is { })
        {
            return $"{prefix}{GetLogLevelString(logLevel)} {message}";
        }
        else if (ex is { })
        {
            return $"{prefix}{GetLogLevelString(logLevel)} {ex}";
        }
        else
        {
            return null;
        }
    }

    protected abstract void OnOutputLog(
        LogLevels logLevel, string? message, Exception? ex);
}
