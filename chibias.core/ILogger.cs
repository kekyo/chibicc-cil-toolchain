/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace chibias;

public enum LogLevels
{
    Debug = 1,
    Trace,
    Information,
    Warning,
    Error,
    Silent = 100,
}

public interface ILogger
{
    void OutputLog(
        LogLevels logLevel, FormattableString? message, Exception? ex);
}

public static class LoggerExtension
{
    public static void Debug(this ILogger logger, FormattableString message) =>
        logger.OutputLog(LogLevels.Debug, message, null);

    public static void Trace(this ILogger logger, FormattableString message) =>
        logger.OutputLog(LogLevels.Trace, message, null);

    public static void Information(this ILogger logger, FormattableString message) =>
        logger.OutputLog(LogLevels.Information, message, null);

    public static void Warning(this ILogger logger, FormattableString message) =>
        logger.OutputLog(LogLevels.Warning, message, null);
    public static void Warning(this ILogger logger, Exception ex) =>
        logger.OutputLog(LogLevels.Warning, null, ex);
    public static void Warning(this ILogger logger, Exception ex, FormattableString message) =>
        logger.OutputLog(LogLevels.Warning, message, ex);

    public static void Error(this ILogger logger, FormattableString message) =>
        logger.OutputLog(LogLevels.Error, message, null);
    public static void Error(this ILogger logger, Exception ex) =>
        logger.OutputLog(LogLevels.Error, null, ex);
    public static void Error(this ILogger logger, Exception ex, FormattableString message) =>
        logger.OutputLog(LogLevels.Error, message, ex);
}

public abstract class LoggerBase : ILogger
{
    public readonly LogLevels BaseLevel;

    protected LoggerBase(LogLevels baseLevel) =>
        this.BaseLevel = baseLevel;

    public void OutputLog(
        LogLevels logLevel, FormattableString? message, Exception? ex)
    {
        if (logLevel >= this.BaseLevel)
        {
            this.OnOutputLog(logLevel, message, ex);
        }
    }

    protected virtual string? ToString(
        string? header, LogLevels logLevel, FormattableString? message, Exception? ex)
    {
        static string GetLogLevelString(LogLevels logLevel) =>
            logLevel != LogLevels.Information ? $" {logLevel.ToString().ToLowerInvariant()}:" : "";

        if (message is { } && ex is { })
        {
            return $"chibias:{GetLogLevelString(logLevel)} {message}, {ex}";
        }
        else if (message is { })
        {
            return $"chibias:{GetLogLevelString(logLevel)} {message}";
        }
        else if (ex is { })
        {
            return $"chibias:{GetLogLevelString(logLevel)} {ex}";
        }
        else
        {
            return null;
        }
    }

    protected abstract void OnOutputLog(
        LogLevels logLevel, FormattableString? message, Exception? ex);
}

public sealed class TextWriterLogger : LoggerBase, IDisposable
{
    public readonly TextWriter Writer;

    private readonly string? header;

    public TextWriterLogger(LogLevels baseLevel, TextWriter tw, string? header = null) :
        base(baseLevel)
    {
        this.Writer = tw;
        this.header = header;
    }

    public void Dispose() =>
        this.Writer.Flush();

    protected override void OnOutputLog(
        LogLevels logLevel, FormattableString? message, Exception? ex)
    {
        if (base.ToString(this.header, logLevel, message, ex) is { } formatted)
        {
            this.Writer.WriteLine(formatted);
        }
    }
}
