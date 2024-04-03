/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using chibild.cli;

namespace chibild;

public static class LinkerExtension
{
    public static bool Link(
        this Linker linker,
        string outputAssemblyPath,
        string[] referenceAssemblyBasePaths,
        ILibraryReference[] libraryReferences,
        AssemblyTypes assemblyType,
        DebugSymbolTypes debugSymbolType,
        AssembleOptions options,
        Version version,
        TargetFramework targetFramework,
        string? injectToAssemblyPath,
        params string[] sourcePaths) =>
        linker.Link(
            outputAssemblyPath,
            new()
            {
                LibraryReferenceBasePaths = referenceAssemblyBasePaths,
                LibraryReferences = libraryReferences,
                CreationOptions = new()
                {
                    Options = options,
                    AssemblyType = assemblyType,
                    Version = version,
                    TargetFramework = targetFramework,
                },
                DebugSymbolType = debugSymbolType,
            },
            injectToAssemblyPath,
            sourcePaths);

    public static bool Link(
        this Linker linker,
        CliOptions cilOptions) =>
        linker.Link(
            cilOptions.OutputAssemblyPath,
            cilOptions.LinkerOptions,
            cilOptions.InjectToAssemblyPath,
            cilOptions.InputFilePaths.ToArray());
}
