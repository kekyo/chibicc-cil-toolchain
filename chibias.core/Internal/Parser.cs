/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace chibias.Internal;

internal sealed partial class Parser
{
    private static readonly FileDescriptor unknown = 
        new(null, "unknown.s", DocumentLanguage.Cil);

    private readonly ILogger logger;
    private readonly ModuleDefinition module;
    private readonly TypeDefinition cabiTextType;
    private readonly TypeDefinition cabiRDataType;
    private readonly Dictionary<string, IMemberDefinition> cabiSpecificSymbols;
    private readonly Lazy<Dictionary<string, TypeDefinition>> referenceTypes;
    private readonly bool produceExecutable;
    private readonly bool produceDebuggingInformation;
    private readonly Dictionary<string, TypeReference> knownTypes = new();
    private readonly Dictionary<string, Instruction> labelTargets = new();
    private readonly List<MethodDefinition> initializers = new();
    private readonly Dictionary<int, TypeDefinition> constantTypes = new();
    private readonly Dictionary<string, FileDescriptor> files = new();
    private readonly Dictionary<Instruction, Location> locationByInstructions = new();
    private readonly List<string> willApplyLabelingNames = new();
    private readonly List<Action> delayedLookupBranchTargetActions = new();
    private readonly List<Action> delayedLookupLocalMemberActions = new();
    private readonly Dictionary<string, List<VariableDebugInformation>> variableDebugInformationLists = new();
    private readonly Lazy<TypeReference> valueType;

    private int placeholderIndex;
    private FileDescriptor currentFile;
    private Location? queuedLocation;
    private Location? lastLocation;
    private bool isProducedOriginalSourceCodeLocation = true;
    private MethodDefinition? method;
    private MethodBody? body;
    private ICollection<Instruction>? instructions;
    private TypeDefinition? structure;
    private bool caughtError;

    /////////////////////////////////////////////////////////////////////

    public Parser(
        ILogger logger,
        ModuleDefinition module,
        Dictionary<string, IMemberDefinition> cabiSpecificSymbols,
        TypeDefinition[] referenceTypes,
        bool produceExecutable,
        bool produceDebuggingInformation)
    {
        this.logger = logger;
        
        this.module = module;

        this.cabiTextType = new TypeDefinition(
            "C",
            "text",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed |
            TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            this.module.TypeSystem.Object);
        this.cabiRDataType = new TypeDefinition(
            "C",
            "rdata",
            TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed |
            TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            this.module.TypeSystem.Object);

        this.cabiSpecificSymbols = cabiSpecificSymbols;
        this.referenceTypes = new(() => referenceTypes.
            ToDictionary(type => type.FullName));
        this.produceExecutable = produceExecutable;
        this.produceDebuggingInformation = produceDebuggingInformation;
        this.valueType = new(() =>
            this.Import(this.referenceTypes.Value["System.ValueType"]));
        this.currentFile = unknown;

        // Known types
        this.knownTypes.Add("void", module.TypeSystem.Void);
        this.knownTypes.Add("uint8", module.TypeSystem.Byte);
        this.knownTypes.Add("int8", module.TypeSystem.SByte);
        this.knownTypes.Add("int16", module.TypeSystem.Int16);
        this.knownTypes.Add("uint16", module.TypeSystem.UInt16);
        this.knownTypes.Add("int32", module.TypeSystem.Int32);
        this.knownTypes.Add("uint32", module.TypeSystem.UInt32);
        this.knownTypes.Add("int64", module.TypeSystem.Int64);
        this.knownTypes.Add("uint64", module.TypeSystem.UInt64);
        this.knownTypes.Add("float32", module.TypeSystem.Single);
        this.knownTypes.Add("float64", module.TypeSystem.Double);
        this.knownTypes.Add("intptr", module.TypeSystem.IntPtr);
        this.knownTypes.Add("uintptr", module.TypeSystem.UIntPtr);
        this.knownTypes.Add("bool", module.TypeSystem.Boolean);
        this.knownTypes.Add("char", module.TypeSystem.Char);
        this.knownTypes.Add("object", module.TypeSystem.Object);
        this.knownTypes.Add("string", module.TypeSystem.String);
        this.knownTypes.Add("typeref", module.TypeSystem.TypedReference);

        // Aliases
        this.knownTypes.Add("byte", module.TypeSystem.Byte);
        this.knownTypes.Add("sbyte", module.TypeSystem.SByte);
        this.knownTypes.Add("short", module.TypeSystem.Int16);
        this.knownTypes.Add("ushort", module.TypeSystem.UInt16);
        this.knownTypes.Add("int", module.TypeSystem.Int32);
        this.knownTypes.Add("uint", module.TypeSystem.UInt32);
        this.knownTypes.Add("long", module.TypeSystem.Int64);
        this.knownTypes.Add("ulong", module.TypeSystem.UInt64);
        this.knownTypes.Add("single", module.TypeSystem.Single);
        this.knownTypes.Add("float", module.TypeSystem.Single);
        this.knownTypes.Add("double", module.TypeSystem.Double);
        this.knownTypes.Add("nint", module.TypeSystem.IntPtr);
        this.knownTypes.Add("nuint", module.TypeSystem.UIntPtr);
        this.knownTypes.Add("char16", module.TypeSystem.Char);
    }

    /////////////////////////////////////////////////////////////////////

    public void SetSourcePathDebuggerHint(string? basePath, string relativePath)
    {
        this.currentFile = new(basePath, relativePath, DocumentLanguage.Cil);
        this.isProducedOriginalSourceCodeLocation = true;
        this.queuedLocation = null;
        this.lastLocation = null;
    }

    /////////////////////////////////////////////////////////////////////

    private void OutputError(Token token, string message)
    {
        this.caughtError = true;
        this.logger.Error($"{this.currentFile.RelativePath}({token.Line + 1},{token.StartColumn + 1}): {message}");
    }

    private void OutputError(Location location, string message)
    {
        this.caughtError = true;
        this.logger.Error($"{location.File.RelativePath}({location.StartLine + 1},{location.StartColumn + 1}): {message}");
    }

    private void OutputTrace(string message) =>
        this.logger.Trace($"{message}");

    /////////////////////////////////////////////////////////////////////

    private MethodDefinition CreateDummyMethod() =>
        new($"<placeholder_method>_${placeholderIndex++}",
            MethodAttributes.Private | MethodAttributes.Abstract,
            this.module.TypeSystem.Void);

    private FieldDefinition CreateDummyField() =>
        new($"<placeholder_field>_${placeholderIndex++}",
            FieldAttributes.Private | FieldAttributes.InitOnly,
            this.module.TypeSystem.Int32);

    private TypeDefinition CreateDummyType() =>
        new("", $"<placeholder_type>_${placeholderIndex++}",
            TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);

    private TypeReference Import(TypeReference type) =>
        (type.Module?.Equals(this.module) ?? type is TypeDefinition) ?
            type : this.module.ImportReference(type);

    private MethodReference Import(MethodReference method) =>
        (method.Module?.Equals(this.module) ?? method is MethodDefinition) ?
            method : this.module.ImportReference(method);

    private FieldReference Import(FieldReference field) =>
        (field.Module?.Equals(this.module) ?? field is FieldDefinition) ?
            field : this.module.ImportReference(field);

    /////////////////////////////////////////////////////////////////////

    private bool TryGetType(
        string name,
        out TypeReference type)
    {
        switch (name[name.Length - 1])
        {
            case '*':
                if (this.TryGetType(name.Substring(0, name.Length - 1), out var preType1))
                {
                    type = new PointerType(this.Import(preType1));
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
            case '&':
                if (this.TryGetType(name.Substring(0, name.Length - 1), out var preType2))
                {
                    type = new ByReferenceType(this.Import(preType2));
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
            case ']' when name.Length >= 2 && name[name.Length - 2] == '[':
                if (this.TryGetType(name.Substring(0, name.Length - 2), out var preType3))
                {
                    type = new ArrayType(this.Import(preType3));
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
            default:
                // IMPORTANT ORDER:
                //   Will lookup before this module, because the types redefinition by C headers
                //   each assembly (by generating chibias).
                //   Always we use first finding type, silently ignored when multiple declarations.
                if (this.cabiSpecificSymbols.TryGetValue(name, out var member) &&
                    member is TypeReference tr1)
                {
                    type = this.Import(tr1);
                    return true;
                }
                else if (this.module.Types.
                    Where(type => type.Namespace == "C.type").
                    FirstOrDefault(type => type.Name == name) is { } td2)
                {
                    type = td2;
                    return true;
                }
                else if (this.knownTypes.TryGetValue(name, out type!))
                {
                    return true;
                }
                else if (this.referenceTypes.Value.TryGetValue(name, out var td3))
                {
                    type = this.Import(td3);
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
        }
    }

    private bool TryGetMethod(
        string name, string[] parameterTypeNames, out MethodReference method)
    {
        var methodNameIndex = name.LastIndexOf('.');
        var methodName = name.Substring(methodNameIndex + 1);
        if (methodNameIndex <= 0)
        {
            if (this.cabiSpecificSymbols.TryGetValue(
                methodName, out var member) &&
                member is MethodReference m &&
                parameterTypeNames.Length == 0)
            {
                method = this.Import(m);
                return true;
            }
            else if (this.cabiTextType.Methods.
                FirstOrDefault(method => method.Name == methodName) is { } m2)
            {
                // In this case, we do not check matching any parameter types.
                // Because this is in CABI specific.
                method = m2;
                return true;
            }
            else
            {
                method = null!;
                return false;
            }
        }

        var typeName = name.Substring(0, methodNameIndex);

        if (!this.referenceTypes.Value.TryGetValue(typeName, out var type))
        {
            method = null!;
            return false;
        }

        var strictParameterTypeNames = parameterTypeNames.
            Select(parameterTypeName => this.TryGetType(parameterTypeName, out var type) ? type.FullName : string.Empty).
            ToArray();

        if (strictParameterTypeNames.Contains(string.Empty))
        {
            method = null!;
            return false;
        }

        // Take only public method at imported.
        if (type.Methods.FirstOrDefault(method =>
            method.IsPublic && method.Name == methodName &&
            strictParameterTypeNames.SequenceEqual(
                method.Parameters.Select(p => p.ParameterType.FullName))) is { } m3)
        {
            method = this.Import(m3);
            return true;
        }
        else
        {
            method = null!;
            return false;
        }
    }

    private bool TryGetField(
        string name, out FieldReference field)
    {
        var fieldNameIndex = name.LastIndexOf('.');
        var fieldName = name.Substring(fieldNameIndex + 1);
        if (fieldNameIndex <= 0)
        {
            if (this.cabiSpecificSymbols.TryGetValue(
                name, out var member) &&
                member is FieldReference f)
            {
                field = this.Import(f);
                return true;
            }
            else if (this.cabiTextType.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f2)
            {
                field = f2;
                return true;
            }
            else if (this.cabiRDataType.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f3)
            {
                field = f3;
                return true;
            }
            else
            {
                field = null!;
                return false;
            }
        }

        var typeName = name.Substring(0, fieldNameIndex);

        if (!this.referenceTypes.Value.TryGetValue(typeName, out var type))
        {
            field = null!;
            return false;
        }

        // Take only public field at imported.
        if (type.Fields.FirstOrDefault(field =>
            field.IsPublic && field.Name == fieldName) is { } f4)
        {
            field = this.Import(f4);
            return true;
        }
        else
        {
            field = null!;
            return false;
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void DelayLookingUpType(
        string typeName,
        Token typeNameToken,
        Action<TypeReference> action) =>
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetType(typeName, out var type))
            {
                action(type);
            }
            else
            {
                this.OutputError(
                    typeNameToken,
                    $"Could not find type: {typeName}");
            }
        });

    private void DelayLookingUpType(
        Token typeNameToken,
        Action<TypeReference> action) =>
        this.DelayLookingUpType(
            typeNameToken.Text,
            typeNameToken,
            action);

    private void DelayLookingUpType(
        string typeName,
        Location location,
        Action<TypeReference> action) =>
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetType(typeName, out var type))
            {
                action(type);
            }
            else
            {
                this.OutputError(
                    location,
                    $"Could not find type: {typeName}");
            }
        });

    private void DelayLookingUpField(
        Token fieldNameToken,
        Action<FieldReference> action) =>
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(fieldNameToken.Text, out var field))
            {
                action(field);
            }
            else
            {
                this.OutputError(
                    fieldNameToken,
                    $"Could not find type: {fieldNameToken.Text}");
            }
        });

    private void DelayLookingUpField(
        string fieldName,
        Location location,
        Action<FieldReference> action) =>
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(fieldName, out var field))
            {
                action(field);
            }
            else
            {
                this.OutputError(
                    location,
                    $"Could not find field: {fieldName}");
            }
        });

    private void DelayLookingUpMethod(
        Token methodNameToken,
        string[] parameterTypeNames,
        Action<MethodReference> action) =>
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMethod(
                methodNameToken.Text, parameterTypeNames, out var method))
            {
                action(method);
            }
            else
            {
                this.OutputError(
                    methodNameToken,
                    $"Could not find type: {methodNameToken.Text}");
            }
        });

    private void DelayLookingUpMethod(
        string methodName,
        string[] parameterTypeNames,
        Location location,
        Action<MethodReference> action) =>
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMethod(
                methodName, parameterTypeNames, out var method))
            {
                action(method);
            }
            else
            {
                this.OutputError(
                    location,
                    $"Could not find type: {methodName}");
            }
        });

    /////////////////////////////////////////////////////////////////////

    private void ParseLabel(Token token)
    {
        if (this.instructions == null)
        {
            this.OutputError(token, $"Function directive is not defined.");
        }
        else
        {
            this.willApplyLabelingNames.Add(token.Text);
        }
    }

    /////////////////////////////////////////////////////////////////////

    public void Parse(Token[] tokens)
    {
        if (tokens.FirstOrDefault() is { } token0)
        {
            switch (token0.Type)
            {
                // Is it an assembler directive?
                case TokenTypes.Directive:
                    this.ParseDirective(token0, tokens);
                    break;
                // Is it a label?
                case TokenTypes.Label:
                    this.ParseLabel(token0);
                    break;
                // Is it an OpCode?
                case TokenTypes.Identity
                    when this.instructions != null &&
                         Utilities.TryParseOpCode(token0.Text, out var opCode):
                    this.ParseInstruction(opCode, tokens);
                    break;
                // Is it a structure member?
                case TokenTypes.Identity
                    when this.structure != null:
                    this.ParseStructureMember(tokens);
                    break;
                // Other, invalid syntax.
                default:
                    this.OutputError(token0, $"Invalid syntax.");
                    break;
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void FinishCurrentState()
    {
        if (this.method != null)
        {
            Debug.Assert(this.instructions != null);
            Debug.Assert(this.body != null);
            Debug.Assert(this.structure == null);

            if (!this.caughtError)
            {
                foreach (var action in this.delayedLookupBranchTargetActions)
                {
                    action();
                }
            }

            this.delayedLookupBranchTargetActions.Clear();
            this.labelTargets.Clear();
            this.willApplyLabelingNames.Clear();
            this.instructions = null;
            this.body = null;
            this.method = null;
        }

        if (this.structure != null)
        {
            Debug.Assert(this.method == null);
            Debug.Assert(this.instructions == null);
            Debug.Assert(this.body == null);
            
            Debug.Assert(this.delayedLookupBranchTargetActions.Count == 0);
            Debug.Assert(this.labelTargets.Count == 0);
            Debug.Assert(this.willApplyLabelingNames.Count == 0);

            this.structure = null;
        }
    }

    public bool Finish(bool applyOptimization)
    {
        this.FinishCurrentState();

        // main entry point lookup.
        if (this.produceExecutable)
        {
            if (this.cabiTextType.Methods.
                FirstOrDefault(m => m.Name == "main") is { } main)
            {
                this.module.EntryPoint = main;
            }
            else
            {
                this.caughtError = true;
                this.logger.Error($"{this.currentFile.RelativePath}(1,1): Could not find main entry point.");
            }
        }

        if (!this.caughtError)
        {
            if (this.cabiTextType.NestedTypes.Count >= 1 ||
                this.cabiTextType.Fields.Count >= 1 ||
                this.cabiTextType.Methods.Count >= 1)
            {
                this.module.Types.Add(this.cabiTextType);
            }

            if (this.cabiRDataType.NestedTypes.Count >= 1 ||
                this.cabiRDataType.Fields.Count >= 1)
            {
                this.module.Types.Add(this.cabiRDataType);
            }

            // Append type initializer
            if (this.initializers.Count >= 1)
            {
                var typeInitializer = new MethodDefinition(
                    ".cctor",
                    MethodAttributes.Private |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig |
                    MethodAttributes.SpecialName |
                    MethodAttributes.RTSpecialName,
                    this.module.TypeSystem.Void);
                this.cabiTextType.Methods.Add(typeInitializer);

                var body = typeInitializer.Body;
                var instructions = body.Instructions;

                foreach (var initializer in this.initializers)
                {
                    instructions.Add(Instruction.Create(OpCodes.Call, initializer));
                }

                instructions.Add(Instruction.Create(OpCodes.Ret));
            }

            // Fire local member lookup.
            foreach (var action in this.delayedLookupLocalMemberActions)
            {
                action();
            }

            // (Completed all CIL implementations in this place.)

            ///////////////////////////////////////////////

            var documents = new Dictionary<string, Document>();
            foreach (var method in this.cabiTextType.Methods)
            {
                if (method.Body.Instructions.Count >= 1)
                {
                    // Apply optimization.
                    if (applyOptimization)
                    {
                        method.Body.Optimize();
                    }

                    // After optimization, the instructions maybe changed absolute layouts,
                    // so we could set debugging information scope after that.
                    if (this.produceDebuggingInformation)
                    {
                        method.DebugInformation.Scope = new ScopeDebugInformation(
                            method.Body.Instructions.First(),
                            method.Body.Instructions.Last());

                        // Will make sequence points:
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if (this.locationByInstructions.TryGetValue(instruction, out var location))
                            {
                                if (!documents.TryGetValue(location.File.RelativePath, out var document))
                                {
                                    document = new(location.File.BasePath is { } basePath ?
                                        Path.Combine(basePath, location.File.RelativePath) :
                                        location.File.RelativePath);
                                    document.Type = DocumentType.Text;
                                    if (location.File.Language is { } language)
                                    {
                                        document.Language = language;
                                    }
                                    documents.Add(location.File.RelativePath, document);
                                }

                                var sequencePoint = new SequencePoint(
                                    instruction, document);

                                sequencePoint.StartLine = location.StartLine + 1;
                                sequencePoint.StartColumn = location.StartColumn + 1;
                                sequencePoint.EndLine = location.EndLine + 1;
                                sequencePoint.EndColumn = location.EndColumn + 1;

                                method.DebugInformation.SequencePoints.Add(
                                    sequencePoint);
                            }
                        }

                        // Will make local variable naming.
                        if (this.variableDebugInformationLists.TryGetValue(method.Name, out var list))
                        {
                            foreach (var variableDebugInformation in list)
                            {
                                method.DebugInformation.Scope.Variables.Add(
                                    variableDebugInformation);
                            }
                        }
                    }
                }
            }
        }

        this.delayedLookupLocalMemberActions.Clear();
        this.files.Clear();
        this.locationByInstructions.Clear();
        this.variableDebugInformationLists.Clear();
        this.initializers.Clear();
        this.constantTypes.Clear();

        this.isProducedOriginalSourceCodeLocation = true;
        this.currentFile = unknown;
        this.queuedLocation = null;
        this.lastLocation = null;

        var finished = !this.caughtError;
        this.caughtError = false;

        return finished;
    }
}
