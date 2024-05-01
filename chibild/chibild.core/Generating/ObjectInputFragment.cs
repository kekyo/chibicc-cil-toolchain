/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Parsing;
using chibild.Internal;
using Mono.Cecil;
using System.Collections.Generic;

namespace chibild.Generating;

internal abstract class ObjectInputFragment :
    InputFragment
{
    private readonly Dictionary<string, TypeDefinition> enumerationDeclarations = new();
    private readonly Dictionary<string, TypeDefinition> fileEnumerationDeclarations = new();
    private readonly Dictionary<string, TypeDefinition> structureDeclarations = new();
    private readonly Dictionary<string, TypeDefinition> fileStructureDeclarations = new();
    private readonly Dictionary<string, FieldDefinition> variableDeclarations = new();
    private readonly Dictionary<string, FieldDefinition> fileVariableDeclarations = new();
    private readonly Dictionary<string, FieldDefinition> constantDeclarations = new();
    private readonly Dictionary<string, FieldDefinition> fileConstantDeclarations = new();
    private readonly Dictionary<string, MethodDefinition> functionDeclarations = new();
    private readonly Dictionary<string, MethodDefinition> fileFunctionDeclarations = new();
    private readonly List<MethodDefinition> moduleFunctionDeclarations = new();
    private readonly List<MethodDefinition> initializerDeclaraions = new();
    private readonly List<MethodDefinition> fileInitializerDeclaraions = new();

    //////////////////////////////////////////////////////////////

    protected ObjectInputFragment(
        string baseInputPath,
        string relativePath) :
        base(baseInputPath, relativePath)
    {
    }

    //////////////////////////////////////////////////////////////

    public abstract GlobalVariableNode[] GlobalVariables { get; }

    public abstract GlobalConstantNode[] GlobalConstants { get; }

    public abstract FunctionNode[] Functions { get; }

    public abstract InitializerNode[] Initializers { get; }

    public abstract EnumerationNode[] Enumerations { get; }

    public abstract StructureNode[] Structures { get; }

    //////////////////////////////////////////////////////////////

    public override bool TryGetType(
        TypeNode type,
        ModuleDefinition fallbackModule,
        out TypeReference tr)
    {
        var typeIdentity = type.TypeIdentity;
        if (this.fileEnumerationDeclarations.TryGetValue(typeIdentity, out var fed))
        {
            tr = fed;
            return true;
        }
        if (this.fileStructureDeclarations.TryGetValue(typeIdentity, out var fsd))
        {
            tr = fsd;
            return true;
        }
        if (this.enumerationDeclarations.TryGetValue(typeIdentity, out var ed))
        {
            tr = ed;
            return true;
        }
        if (this.structureDeclarations.TryGetValue(typeIdentity, out var sd))
        {
            tr = sd;
            return true;
        }

        tr = null!;
        return false;
    }

    public override bool TryGetField(
        IdentityNode variable,
        ModuleDefinition fallbackModule,
        out FieldReference fr)
    {
        var variableName = variable.Identity;
        if (this.fileVariableDeclarations.TryGetValue(variableName, out var fvd))
        {
            fr = fvd;
            return true;
        }
        if (this.fileConstantDeclarations.TryGetValue(variableName, out var fcd))
        {
            fr = fcd;
            return true;
        }
        if (this.variableDeclarations.TryGetValue(variableName, out var vd))
        {
            fr = vd;
            return true;
        }
        if (this.constantDeclarations.TryGetValue(variableName, out var cd))
        {
            fr = cd;
            return true;
        }

        fr = null!;
        return false;
    }

    public override bool TryGetMethod(
        IdentityNode function,
        FunctionSignatureNode? signature,
        ModuleDefinition fallbackModule,
        out MethodReference mr)
    {
        // Ignored the signature, because contains only CABI functions.
        var functionName = function.Identity;
        if (this.fileFunctionDeclarations.TryGetValue(functionName, out var fmd))
        {
            mr = fmd;
            return true;
        }
        if (this.functionDeclarations.TryGetValue(functionName, out var md))
        {
            mr = md;
            return true;
        }

        mr = null!;
        return false;
    }

    //////////////////////////////////////////////////////////////

    public bool TryAddEnumeration(TypeDefinition enumeration, bool isFileScope)
    {
        if (isFileScope)
        {
            lock (this.fileEnumerationDeclarations)
            {
                return this.fileEnumerationDeclarations.TryAdd(
                    enumeration.Name, enumeration);
            }
        }
        else
        {
            lock (this.enumerationDeclarations)
            {
                return this.enumerationDeclarations.TryAdd(
                    enumeration.Name, enumeration);
            }
        }
    }

    public bool TryAddStructure(TypeDefinition structure, bool isFileScope)
    {
        if (isFileScope)
        {
            lock (this.fileStructureDeclarations)
            {
                return this.fileStructureDeclarations.TryAdd(
                    structure.Name, structure);
            }
        }
        else
        {
            lock (this.structureDeclarations)
            {
                return this.structureDeclarations.TryAdd(
                    structure.Name, structure);
            }
        }
    }

    public void AddVariable(FieldDefinition variable, bool isFileScope)
    {
        if (isFileScope)
        {
            lock (this.fileVariableDeclarations)
            {
                this.fileVariableDeclarations.Add(variable.Name, variable);
            }
        }
        else
        {
            lock (this.variableDeclarations)
            {
                this.variableDeclarations.Add(variable.Name, variable);
            }
        }
    }
    
    public void AddConstant(FieldDefinition constant, bool isFileScope)
    {
        if (isFileScope)
        {
            lock (this.fileConstantDeclarations)
            {
                this.fileConstantDeclarations.Add(constant.Name, constant);
            }
        }
        else
        {
            lock (this.constantDeclarations)
            {
                this.constantDeclarations.Add(constant.Name, constant);
            }
        }
    }
    
    public void AddFunction(MethodDefinition function, bool isFileScope)
    {
        if (isFileScope)
        {
            lock (this.fileFunctionDeclarations)
            {
                this.fileFunctionDeclarations.Add(function.Name, function);
            }
        }
        else
        {
            lock (this.functionDeclarations)
            {
                this.functionDeclarations.Add(function.Name, function);
            }
        }
    }
    
    public void AddModuleFunction(MethodDefinition function)
    {
        lock (this.moduleFunctionDeclarations)
        {
            this.moduleFunctionDeclarations.Add(function);
        }
    }
    
    public void AddInitializer(MethodDefinition method, bool isFileScope)
    {
        if (isFileScope)
        {
            lock (this.fileInitializerDeclaraions)
            {
                this.fileInitializerDeclaraions.Add(method);
            }
        }
        else
        {
            lock (this.initializerDeclaraions)
            {
                this.initializerDeclaraions.Add(method);
            }
        }
    }

    //////////////////////////////////////////////////////////////

    public IEnumerable<TypeDefinition> GetDeclaredEnumerations(bool isFileScope) =>
        isFileScope ?
            this.fileEnumerationDeclarations.Values :
            this.enumerationDeclarations.Values;

    public IEnumerable<TypeDefinition> GetDeclaredStructures(bool isFileScope) =>
        isFileScope ?
            this.fileStructureDeclarations.Values :
            this.structureDeclarations.Values;

    public IEnumerable<FieldDefinition> GetDeclaredVariables(bool isFileScope) =>
        isFileScope ?
            this.fileVariableDeclarations.Values :
            this.variableDeclarations.Values;

    public IEnumerable<FieldDefinition> GetDeclaredConstants(bool isFileScope) =>
        isFileScope ?
            this.fileConstantDeclarations.Values :
            this.constantDeclarations.Values;

    public IEnumerable<MethodDefinition> GetDeclaredFunctions(bool isFileScope) =>
        isFileScope ?
            this.fileFunctionDeclarations.Values :
            this.functionDeclarations.Values;

    public IEnumerable<MethodDefinition> GetDeclaredModuleFunctions() =>
        this.moduleFunctionDeclarations;

    public IEnumerable<MethodDefinition> GetDeclaredInitializer(bool isFileScope) =>
        isFileScope ?
            this.fileInitializerDeclaraions :
            this.initializerDeclaraions;
}
