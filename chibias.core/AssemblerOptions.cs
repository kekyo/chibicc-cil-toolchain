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

namespace chibias;

public enum AssemblyTypes
{
    Dll,
    Exe,
    WinExe,
}

public enum DebugSymbolTypes
{
    None,
    Portable,
    Embedded,
    Mono,
    WindowsProprietary,
}

[Flags]
public enum AssembleOptions
{
    None = 0x00,
    ApplyOptimization = 0x01,
    Deterministic = 0x02,
}

public sealed class AssemblerOptions
{
    public string[] ReferenceAssemblyPaths = Utilities.Empty<string>();
    public AssemblyTypes AssemblyType = AssemblyTypes.Exe;
    public DebugSymbolTypes DebugSymbolType = DebugSymbolTypes.Embedded;
    public AssembleOptions Options = AssembleOptions.Deterministic;
    public Version Version = new Version(1, 0, 0, 0);
    public string TargetFrameworkMoniker = ThisAssembly.AssemblyMetadata.TargetFramework;
}
