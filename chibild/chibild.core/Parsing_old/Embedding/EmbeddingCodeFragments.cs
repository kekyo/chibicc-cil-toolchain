/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System;
using System.IO;
using System.Linq;

namespace chibild.Parsing.Embedding;

internal static class EmbeddingCodeFragments
{
    private static readonly string startupName =
        "chibild.Parsing.Embedding._start";

    private static readonly Lazy<Token[][]> startup_void = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Linker).Assembly.GetManifestResourceStream(
            startupName + "_v.s")!)).
        ToArray());
    private static readonly Lazy<Token[][]> startup_int32 = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Linker).Assembly.GetManifestResourceStream(
            startupName + "_i.s")!)).
        ToArray());

    private static readonly Lazy<Token[][]> startup_void_void = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Linker).Assembly.GetManifestResourceStream(
            startupName + "_v_v.s")!)).
        ToArray());
    private static readonly Lazy<Token[][]> startup_int32_void = new(() =>
        Tokenizer.TokenizeAll(new StreamReader(
            typeof(Linker).Assembly.GetManifestResourceStream(
            startupName + "_i_v.s")!)).
        ToArray());

    public static Token[][] Startup_Void =>
        startup_void.Value;
    public static Token[][] Startup_Int32 =>
        startup_int32.Value;
    public static Token[][] Startup_Void_Void =>
        startup_void_void.Value;
    public static Token[][] Startup_Int32_Void =>
        startup_int32_void.Value;
}
