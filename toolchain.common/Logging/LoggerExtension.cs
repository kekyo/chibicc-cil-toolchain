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
using System.Runtime.CompilerServices;

namespace chibicc.toolchain.Logging;

public interface IDisposableLogger : ILogger, IDisposable
{
}

public static class LoggerExtension
{
    public static void Debug(this ILogger logger, string message) =>
        logger.OutputLog(LogLevels.Debug, message, null);

    public static void Trace(this ILogger logger, string message) =>
        logger.OutputLog(LogLevels.Trace, message, null);

    public static void Information(this ILogger logger, string message) =>
        logger.OutputLog(LogLevels.Information, message, null);

    public static void Warning(this ILogger logger, string message) =>
        logger.OutputLog(LogLevels.Warning, message, null);
    public static void Warning(this ILogger logger, Exception ex) =>
        logger.OutputLog(LogLevels.Warning, null, ex);
    public static void Warning(this ILogger logger, Exception ex, string message) =>
        logger.OutputLog(LogLevels.Warning, message, ex);

    public static void Error(this ILogger logger, string message) =>
        logger.OutputLog(LogLevels.Error, message, null);
    public static void Error(this ILogger logger, Exception ex) =>
        logger.OutputLog(LogLevels.Error, null, ex);
    public static void Error(this ILogger logger, Exception ex, string message) =>
        logger.OutputLog(LogLevels.Error, message, ex);
    
    public static IDisposableLogger BeginScope(
        this ILogger logger,
        LogLevels logLevel,
        [CallerMemberName] string memberName = null!)
    {
        logger.OutputLog(logLevel, $"{memberName}: Started.", null);
        return new DisposableLogger(logger, logLevel, memberName);
    }

    private sealed class DisposableLogger : IDisposableLogger
    {
        private readonly ILogger parent;
        private readonly LogLevels logLevel;
        private readonly string memberName;
        private readonly Stopwatch sw;

        public DisposableLogger(
            ILogger parent, LogLevels logLevel, string memberName)
        {
            this.parent = parent;
            this.logLevel = logLevel;
            this.memberName = memberName;
            this.sw = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            if (this.sw.IsRunning)
            {
                this.sw.Stop();
                this.parent.OutputLog(
                    this.logLevel,
                    $"{this.memberName}: Exited, Elapsed={this.sw.Elapsed}",
                    null);
            }
        }

        public void OutputLog(
            LogLevels logLevel, string? message, Exception? ex) =>
            this.parent.OutputLog(
                logLevel,
                $"{this.memberName}: {message}, Elapsed={this.sw.Elapsed}",
                ex);
    }
}
