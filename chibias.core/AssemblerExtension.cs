/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;

namespace chibias;

public static class AssemblerExtension
{
    public static bool Assemble(
        this Assembler assembler,
        string outputAssemblyPath,
        string[] referenceAssemblyBasePaths,
        string[] referenceAssemblyNames,
        AssemblyTypes assemblyType,
        DebugSymbolTypes debugSymbolType,
        AssembleOptions options,
        Version version,
        TargetFramework targetFramework,
        params string[] sourcePaths) =>
        assembler.Assemble(
            outputAssemblyPath,
            new()
            {
                ReferenceAssemblyBasePaths = referenceAssemblyBasePaths,
                ReferenceAssemblyNames = referenceAssemblyNames,
                AssemblyType = assemblyType,
                DebugSymbolType = debugSymbolType,
                Options = options,
                Version = version,
                TargetFramework = targetFramework,
            },
            sourcePaths);
}
