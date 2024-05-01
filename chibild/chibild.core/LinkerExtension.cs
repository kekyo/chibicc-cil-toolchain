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
        this CilLinker linker,
        string outputAssemblyPath,
        string[] referenceAssemblyBasePaths,
        AssemblyTypes assemblyType,
        DebugSymbolTypes debugSymbolType,
        AssembleOptions options,
        Version version,
        TargetFramework targetFramework,
        string? injectToAssemblyPath,
        string baseInputPath,        
        params InputReference[] inputReferences) =>
        linker.Link(
            outputAssemblyPath,
            new()
            {
                LibraryReferenceBasePaths = referenceAssemblyBasePaths,
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
            baseInputPath,
            inputReferences);

    public static bool Link(
        this CilLinker linker,
        CliOptions cilOptions) =>
        linker.Link(
            cilOptions.OutputAssemblyPath,
            cilOptions.LinkerOptions,
            cilOptions.InjectToAssemblyPath,
            cilOptions.BaseInputPath,
            cilOptions.InputReferences);
}
