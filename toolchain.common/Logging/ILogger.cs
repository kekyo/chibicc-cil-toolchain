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

public enum LogLevels
{
    Trace = 1,
    Debug,
    Information,
    Warning,
    Error,
    Silent = 100,
}

public interface ILogger
{
    void OutputLog(
        LogLevels logLevel, string? message, Exception? ex);
}
