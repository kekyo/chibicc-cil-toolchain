/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;

namespace chibicc.toolchain.Logging;

public abstract class LoggerBase : ILogger
{
    public readonly LogLevels BaseLevel;

    protected LoggerBase(LogLevels baseLevel) =>
        this.BaseLevel = baseLevel;

    public void OutputLog(
        LogLevels logLevel, string? message, Exception? ex)
    {
        if (logLevel >= this.BaseLevel)
        {
            this.OnOutputLog(logLevel, message, ex);
        }
    }

    protected virtual string? ToString(
        LogLevels logLevel, string? message, Exception? ex)
    {
        static string GetLogLevelString(LogLevels logLevel) =>
            logLevel != LogLevels.Information ? $" {logLevel.ToString().ToLowerInvariant()}:" : "";

        if (message is { } && ex is { })
        {
            return $"chibild:{GetLogLevelString(logLevel)} {message}, {ex}";
        }
        else if (message is { })
        {
            return $"chibild:{GetLogLevelString(logLevel)} {message}";
        }
        else if (ex is { })
        {
            return $"chibild:{GetLogLevelString(logLevel)} {ex}";
        }
        else
        {
            return null;
        }
    }

    protected abstract void OnOutputLog(
        LogLevels logLevel, string? message, Exception? ex);
}
