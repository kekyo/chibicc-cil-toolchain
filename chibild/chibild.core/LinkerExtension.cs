/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;

namespace chibild;

public static class LinkerExtension
{
    public static bool Link(
        this Linker linker,
        string outputAssemblyPath,
        string[] referenceAssemblyBasePaths,
        string[] referenceAssemblyNames,
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
                ReferenceAssemblyBasePaths = referenceAssemblyBasePaths,
                ReferenceAssemblyNames = referenceAssemblyNames,
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
}
