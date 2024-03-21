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

public enum TargetWindowsArchitectures
{
    AnyCPU,
    Preferred32Bit,
    X86,
    X64,
    IA64,
    ARM,
    ARMv7,
    ARM64,
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
    DisableJITOptimization = 0x01,
}

public enum RuntimeConfigurationOptions
{
    Omit,
    ProduceCoreCLR,
    ProduceCoreCLRMajorRollForward,
    ProduceCoreCLRMinorRollForward,
    ProduceCoreCLRFeatureRollForward,
    ProduceCoreCLRPatchRollForward,
    ProduceCoreCLRLatestMajorRollForward,
    ProduceCoreCLRLatestMinorRollForward,
    ProduceCoreCLRLatestFeatureRollForward,
    ProduceCoreCLRLatestPatchRollForward,
    ProduceCoreCLRDisableRollForward,
}

public sealed class AssemblerCreationOptions
{
    public AssemblyTypes AssemblyType =
        AssemblyTypes.Exe;
    public TargetWindowsArchitectures TargetWindowsArchitecture =
        TargetWindowsArchitectures.AnyCPU;
    public AssembleOptions Options =
        AssembleOptions.DisableJITOptimization;
    public Version Version = new(1, 0, 0, 0);
    public TargetFramework TargetFramework = TargetFramework.Default;
    public RuntimeConfigurationOptions RuntimeConfiguration =
        RuntimeConfigurationOptions.ProduceCoreCLRMajorRollForward;
    public string? AppHostTemplatePath = default;
}

public sealed class AssemblerOptions
{
    public string[] ReferenceAssemblyBasePaths =
        Utilities.Empty<string>();
    public string[] ReferenceAssemblyNames =
        Utilities.Empty<string>();
    public DebugSymbolTypes DebugSymbolType =
        DebugSymbolTypes.Embedded;
    public bool IsDeterministic = true;
    public bool ApplyOptimization = false;
    public AssemblerCreationOptions? CreationOptions =
        new();
}
