/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibild.Internal;
using System;

namespace chibild;

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

public interface ILibraryReference : IEquatable<ILibraryReference>
{
}

public sealed class LibraryNameReference : ILibraryReference
{
    public string Name { get; }

    public LibraryNameReference(string name) =>
        this.Name = name;

    public override string ToString() =>
        $"-l{this.Name}";

    private bool Equals(LibraryNameReference other) =>
        this.Name == other.Name;

    bool IEquatable<ILibraryReference>.Equals(ILibraryReference? other) =>
        this.Equals(other);

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) ||
        obj is LibraryNameReference other &&
        this.Equals(other);

    public override int GetHashCode() =>
        this.Name.GetHashCode();

    public void Deconstruct(out string name) =>
        name = this.Name;
}

public sealed class LibraryPathReference : ILibraryReference
{
    public string Path { get; }

    public LibraryPathReference(string name) =>
        this.Path = name;

    public override string ToString() =>
        this.Path;

    private bool Equals(LibraryPathReference other) =>
        this.Path == other.Path;

    bool IEquatable<ILibraryReference>.Equals(ILibraryReference? other) =>
        this.Equals(other);

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) ||
        obj is LibraryPathReference other &&
        this.Equals(other);

    public override int GetHashCode() =>
        this.Path.GetHashCode();

    public void Deconstruct(out string path) =>
        path = this.Path;
}

public sealed class LinkerOptions
{
    public string[] LibraryReferenceBasePaths =
        Utilities.Empty<string>();
    public ILibraryReference[] LibraryReferences =
        Utilities.Empty<ILibraryReference>();

    public DebugSymbolTypes DebugSymbolType =
        DebugSymbolTypes.Embedded;

    public bool IsDeterministic = true;
    public bool ApplyOptimization = false;

    public AssemblerCreationOptions? CreationOptions =
        new();

    public bool IsDryRun = false;
}
