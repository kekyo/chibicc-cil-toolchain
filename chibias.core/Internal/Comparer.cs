/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Collections.Generic;

namespace chibias.Internal;

internal sealed class AssemblyDefinitionComparer :
    IEqualityComparer<AssemblyDefinition>
{
    public bool Equals(AssemblyDefinition? x, AssemblyDefinition? y) =>
        x!.Name == y!.Name;

    public int GetHashCode(AssemblyDefinition obj) =>
        obj.Name.GetHashCode();

    public static readonly AssemblyDefinitionComparer Instance = new();
}

internal sealed class AssemblyNameReferenceComparer :
    IEqualityComparer<AssemblyNameReference>
{
    public bool Equals(AssemblyNameReference? x, AssemblyNameReference? y) =>
        x!.Name == y!.Name;

    public int GetHashCode(AssemblyNameReference obj) =>
        obj.Name.GetHashCode();

    public static readonly AssemblyNameReferenceComparer Instance = new();
}

internal sealed class ExportedTypeComparer :
    IEqualityComparer<ExportedType>
{
    public bool Equals(ExportedType? x, ExportedType? y) =>
        x!.FullName == y!.FullName;

    public int GetHashCode(ExportedType obj) =>
        obj.FullName.GetHashCode();

    public static readonly ExportedTypeComparer Instance = new();
}

internal sealed class TypeDefinitionComparer :
    IEqualityComparer<TypeDefinition>
{
    public bool Equals(TypeDefinition? x, TypeDefinition? y) =>
        x!.FullName == y!.FullName;

    public int GetHashCode(TypeDefinition obj) =>
        obj.FullName.GetHashCode();

    public static readonly TypeDefinitionComparer Instance = new();
}

