/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.IO;
using System;
using System.IO;
using System.Text;

namespace chibild;

public interface IInputFileItem
{
    string ObjectFilePathDebuggerHint { get; }
    TextReader Open();
}

public class InputTextReaderItem : IInputFileItem
{
    private readonly Func<TextReader> opener;
    
    public InputTextReaderItem(
        Func<TextReader> opener, string objectFilePathDebuggerHint)
    {
        this.opener = opener;
        this.ObjectFilePathDebuggerHint = objectFilePathDebuggerHint;
    }

    public string ObjectFilePathDebuggerHint { get; }

    public TextReader Open() =>
        this.opener();
}

public sealed class InputObjectFileItem : InputTextReaderItem
{
    public InputObjectFileItem(
        string objectFilePath, string objectFilePathDebuggerHint) :
        base(() =>
        {
            var stream = StreamUtilities.OpenStream(objectFilePath, false);
            return new StreamReader(stream, Encoding.UTF8, true, 65536, false);
        }, objectFilePathDebuggerHint)
    {
    }
}

public sealed class InputStdInItem : InputTextReaderItem
{
    public InputStdInItem() :
        base(() => Console.In, "<stdin>")
    {
    }
}
