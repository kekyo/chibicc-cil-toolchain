/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace chibicc.toolchain.Logging;

public sealed class TextWriterLogger : LoggerBase, IDisposable
{
    public readonly TextWriter Writer;

    public TextWriterLogger(LogLevels baseLevel, TextWriter tw) :
        base(baseLevel) =>
        this.Writer = tw;

    public void Dispose() =>
        this.Writer.Flush();

    protected override void OnOutputLog(
        LogLevels logLevel, string? message, Exception? ex)
    {
        if (base.ToString(logLevel, message, ex) is { } formatted)
        {
            this.Writer.WriteLine(formatted);
        }
    }
}
