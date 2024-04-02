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
}
