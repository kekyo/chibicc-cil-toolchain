/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using System;
using System.IO;

namespace chibias.Parsing.Embedding;

internal static class EmbeddingCodeFragments
{
    private static readonly string startupName =
        "chibias.Parsing.Embedding._start";

    private static readonly Lazy<Token[][]> startup_void = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Assembler).Assembly.GetManifestResourceStream(
            startupName + "_v.s")!)));
    private static readonly Lazy<Token[][]> startup_int32 = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Assembler).Assembly.GetManifestResourceStream(
            startupName + "_i.s")!)));

    private static readonly Lazy<Token[][]> startup_void_void = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Assembler).Assembly.GetManifestResourceStream(
            startupName + "_v_v.s")!)));
    private static readonly Lazy<Token[][]> startup_int32_void = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Assembler).Assembly.GetManifestResourceStream(
            startupName + "_i_v.s")!)));

    public static Token[][] Startup_Void =>
        startup_void.Value;
    public static Token[][] Startup_Int32 =>
        startup_int32.Value;
    public static Token[][] Startup_Void_Void =>
        startup_void_void.Value;
    public static Token[][] Startup_Int32_Void =>
        startup_int32_void.Value;
}
