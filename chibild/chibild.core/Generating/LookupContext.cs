/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Parsing;
using chibicc.toolchain.Tokenizing;
using Mono.Cecil;
using System;
using chibild.Internal;

namespace chibild.Generating;

internal sealed class LookupContext
{
    private readonly ModuleDefinition targetModule;

    public readonly ObjectInputFragment? CurrentFragment;
    public readonly InputFragment[] InputFragments;

    public LookupContext(
        ModuleDefinition targetModule,
        ObjectInputFragment? currentFragment,
        InputFragment[] inputFragments)
    {
        this.targetModule = targetModule;
        this.CurrentFragment = currentFragment;
        this.InputFragments = inputFragments;
    }

    public ModuleDefinition FallbackModule =>
        this.targetModule;

    //////////////////////////////////////////////////////////////

    public TypeReference SafeImport(TypeReference tr) =>
        this.targetModule.SafeImport(tr);
        
    public FieldReference SafeImport(FieldReference fr) =>
        this.targetModule.SafeImport(fr);
        
    public MethodReference SafeImport(MethodReference mr) =>
        this.targetModule.SafeImport(mr);

    public MemberReference SafeImport(MemberReference member) =>
        member switch
        {
            TypeReference type => this.SafeImport(type),
            FieldReference field => this.SafeImport(field),
            MethodReference method => this.SafeImport(method),
            _ => throw new InvalidOperationException(),
        };

    //////////////////////////////////////////////////////////////

    public bool UnsafeGetCoreType(
        string coreTypeName,
        out TypeReference tr)
    {
        // This getter is only used for looking up core library types.
        var coreType = new TypeIdentityNode(coreTypeName, Token.Unknown);
        
        if (this.CurrentFragment?.TryGetType(
            coreType,
            this.targetModule,
            out tr) ?? false)
        {
            return true;
        }

        foreach (var fragment in this.InputFragments)
        {
            if (fragment.TryGetType(
                coreType,
                this.targetModule,
                out tr))
            {
                return true;
            }
        }

        tr = null!;
        return false;
    }

    //////////////////////////////////////////////////////////////
    
    public void AddVariable(FieldDefinition variable, bool isFileScope) =>
        this.CurrentFragment!.AddVariable(variable, isFileScope);
    
    public void AddConstant(FieldDefinition constant, bool isFileScope) =>
        this.CurrentFragment!.AddConstant(constant, isFileScope);

    public void AddFunction(MethodDefinition function, bool isFileScope) =>
        this.CurrentFragment!.AddFunction(function, isFileScope);

    public void AddInitializer(MethodDefinition initializer, bool isFileScope) =>
        this.CurrentFragment!.AddInitializer(initializer, isFileScope);

    public void AddModuleFunction(MethodDefinition function) =>
        this.CurrentFragment!.AddModuleFunction(function);

    public bool TryAddEnumeration(TypeDefinition enumeration, bool isFileScope) =>
        this.CurrentFragment!.TryAddEnumeration(enumeration, isFileScope);

    public bool TryAddStructure(TypeDefinition structure, bool isFileScope) =>
        this.CurrentFragment!.TryAddStructure(structure, isFileScope);
}
