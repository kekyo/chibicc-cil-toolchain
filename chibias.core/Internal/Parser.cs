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
using System.Globalization;
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
    private readonly TypeDefinition cabiDataType;
    private readonly MemberDictionary<MemberReference> cabiSpecificSymbols;
    private readonly MemberDictionary<TypeDefinition> referenceTypes;
    private readonly Dictionary<string, TypeReference> knownTypes = new();
    private readonly Dictionary<string, Instruction> labelTargets = new();
    private readonly Dictionary<string, FileDescriptor> files = new();
    private readonly Dictionary<Instruction, Location> locationByInstructions = new();
    private readonly List<string> willApplyLabelingNames = new();
    private readonly List<Action> delayedLookupBranchTargetActions = new();
    private readonly List<Action> delayedLookupLocalMemberActions = new();
    private readonly Dictionary<string, List<VariableDebugInformation>> variableDebugInformationLists = new();
    private readonly Lazy<TypeReference> valueType;
    private readonly Lazy<MethodReference> indexOutOfRangeCtor;
    private readonly bool produceExecutable;
    private readonly bool produceDebuggingInformation;

    private int placeholderIndex;
    private FileDescriptor currentFile;
    private Location? queuedLocation;
    private Location? lastLocation;
    private bool isProducedOriginalSourceCodeLocation = true;
    private MethodDefinition? method;
    private MethodBody? body;
    private ICollection<Instruction>? instructions;
    private TypeDefinition? structureType;
    private int checkingStructureMemberIndex = -1;
    private bool caughtError;

    /////////////////////////////////////////////////////////////////////

    public Parser(
        ILogger logger,
        ModuleDefinition module,
        MemberDictionary<MemberReference> cabiSpecificSymbols,
        TypeDefinitionCache referenceTypes,
        bool produceExecutable,
        bool produceDebuggingInformation)
    {
        this.logger = logger;
        
        this.module = module;

        this.cabiTextType = new TypeDefinition(
            "C",
            "text",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed |
            TypeAttributes.Class,
            this.module.TypeSystem.Object);
        this.cabiDataType = new TypeDefinition(
            "C",
            "data",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed |
            TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            this.module.TypeSystem.Object);

        this.cabiSpecificSymbols = cabiSpecificSymbols;
        this.referenceTypes = new(
            referenceTypes.OfType<TypeDefinition>(),
            type => type.FullName);
        this.produceExecutable = produceExecutable;
        this.produceDebuggingInformation = produceDebuggingInformation;

        // Resolver for runtime members

        TypeReference GetType(string typeName) =>
            this.Import(this.referenceTypes.TryGetMember("System.ValueType", out var type) ? type : null!);

        this.valueType = new(() => GetType("System.ValueType"));
        this.indexOutOfRangeCtor = new(() => this.Import(
            GetType("System.IndexOutOfRangeException").
            Resolve()?.Methods.
            FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0)!));
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

    public bool TryGetType(
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
            case ']' when name.Length >= 4:
                var startBracketIndex = name.LastIndexOf('[', name.Length - 2);
                // "aaa"
                if (startBracketIndex >= 1 &&
                    this.TryGetType(name.Substring(0, startBracketIndex), out var elementType))
                {
                    // "aaa[]"
                    if ((name.Length - startBracketIndex - 2) == 0)
                    {
                        type = new ArrayType(this.Import(elementType));
                        return true;
                    }
                    // "aaa[10]"
                    else
                    {
                        var length = int.Parse(
                            name.Substring(startBracketIndex + 1, name.Length - startBracketIndex - 2),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);

                        // "aaa_len10"
                        type = this.GetValueArrayType(elementType, length);
                        return true;
                    }
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
                if (this.cabiSpecificSymbols.TryGetMember<TypeReference>(name, out var tr1))
                {
                    type = this.Import(tr1);
                    return true;
                }
                else if (this.module.Types.FirstOrDefault(type =>
                    (type.Namespace == "C.type" ? type.Name : type.FullName) == name) is { } td2)
                {
                    type = td2;
                    return true;
                }
                else if (this.knownTypes.TryGetValue(name, out type!))
                {
                    return true;
                }
                else if (this.referenceTypes.TryGetMember(name, out var td3))
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

    public bool TryGetMethod(
        string name, string[] parameterTypeNames, out MethodReference method)
    {
        var methodNameIndex = name.LastIndexOf('.');
        var methodName = name.Substring(methodNameIndex + 1);

        if (methodName == "ctor" || methodName == "cctor")
        {
            methodName = "." + methodName;
            methodNameIndex--;
        }

        if (methodNameIndex <= 0)
        {
            if (this.cabiSpecificSymbols.TryGetMember<MethodReference>(methodName, out var m) &&
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

        if (!this.referenceTypes.TryGetMember(typeName, out var type))
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

    public bool TryGetField(
        string name, out FieldReference field)
    {
        var fieldNameIndex = name.LastIndexOf('.');
        var fieldName = name.Substring(fieldNameIndex + 1);
        if (fieldNameIndex <= 0)
        {
            if (this.cabiSpecificSymbols.TryGetMember<FieldReference>(name, out var f))
            {
                field = this.Import(f);
                return true;
            }
            else if (this.cabiDataType.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f2)
            {
                field = f2;
                return true;
            }
            else
            {
                field = null!;
                return false;
            }
        }

        var typeName = name.Substring(0, fieldNameIndex);

        if (!this.referenceTypes.TryGetMember(typeName, out var type))
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
                    $"Could not find method: {methodNameToken.Text}");
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
                    $"Could not find method: {methodName}");
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
                    when this.structureType != null:
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
            Debug.Assert(this.structureType == null);

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

        if (this.structureType != null)
        {
            Debug.Assert(this.method == null);
            Debug.Assert(this.instructions == null);
            Debug.Assert(this.body == null);
            
            Debug.Assert(this.delayedLookupBranchTargetActions.Count == 0);
            Debug.Assert(this.labelTargets.Count == 0);
            Debug.Assert(this.willApplyLabelingNames.Count == 0);

            if (this.checkingStructureMemberIndex >= 0 &&
                this.checkingStructureMemberIndex < this.structureType.Fields.Count)
            {
                this.caughtError = true;
                this.logger.Error(
                    $"Structure member difference exists before declared type: {this.structureType.Name}");
            }

            this.structureType = null;
            this.checkingStructureMemberIndex = -1;
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
            if (this.cabiTextType.Methods.Count >= 1)
            {
                this.module.Types.Add(this.cabiTextType);
            }

            if (this.cabiDataType.Fields.Count >= 1)
            {
                this.module.Types.Add(this.cabiDataType);
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

        this.isProducedOriginalSourceCodeLocation = true;
        this.currentFile = unknown;
        this.queuedLocation = null;
        this.lastLocation = null;

        var finished = !this.caughtError;
        this.caughtError = false;

        return finished;
    }
}
