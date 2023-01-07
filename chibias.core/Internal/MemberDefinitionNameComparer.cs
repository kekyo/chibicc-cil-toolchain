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

internal sealed class MemberDefinitionNameComparer :
    IEqualityComparer<IMemberDefinition>
{
    private MemberDefinitionNameComparer()
    {
    }

    public bool Equals(IMemberDefinition? x, IMemberDefinition? y) =>
        x is { } && y is { } && x.Name.Equals(y.Name);

    public int GetHashCode(IMemberDefinition obj) =>
        obj.Name.GetHashCode();

    public static readonly MemberDefinitionNameComparer Instance = new();
}
