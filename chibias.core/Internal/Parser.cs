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
    private readonly TargetFramework targetFramework;
    private readonly TypeDefinition cabiTextType;
    private readonly TypeDefinition cabiDataType;
    private readonly MethodDefinition cabiDataTypeInitializer;
    private readonly MemberDictionary<MemberReference> cabiSpecificSymbols;
    private readonly MemberDictionary<TypeDefinition> referenceTypes;
    private readonly Dictionary<string, TypeReference> importantTypes = new();
    private readonly Dictionary<string, Instruction> labelTargets = new();
    private readonly Dictionary<string, FileDescriptor> files = new();
    private readonly Dictionary<Instruction, Location> locationByInstructions = new();
    private readonly List<string> willApplyLabelingNames = new();
    private readonly List<Action> delayedLookupBranchTargetActions = new();
    private readonly List<Action> delayedLookupLocalMemberActions = new();
    private readonly List<Action> delayedCheckAfterLookingupActions = new();
    private readonly Dictionary<string, List<VariableDebugInformation>> variableDebugInformationLists = new();
    private readonly Lazy<TypeReference> systemValueTypeType;
    private readonly Lazy<TypeReference> systemEnumType;
    private readonly Lazy<MethodReference> indexOutOfRangeCtor;
    private readonly bool produceExecutable;
    private readonly bool produceDebuggingInformation;

    private int placeholderIndex;
    private FileDescriptor currentFile;
    private Location? queuedLocation;
    private Location? lastLocation;
    private bool isProducedOriginalSourceCodeLocation = true;
    private TypeDefinition fileScopedType;
    private MethodDefinition fileScopedTypeInitializer;
    private MethodDefinition? method;
    private MethodBody? body;
    private ICollection<Instruction>? instructions;
    private TypeDefinition? structureType;
    private TypeDefinition? enumerationType;
    private TypeReference? enumerationUnderlyingType;
    private EnumerationMemberValueManipulator? enumerationManipulator;
    private int checkingMemberIndex = -1;
    private bool caughtError;

    /////////////////////////////////////////////////////////////////////

    public Parser(
        ILogger logger,
        ModuleDefinition module,
        TargetFramework targetFramework,
        MemberDictionary<MemberReference> cabiSpecificSymbols,
        TypeDefinitionCache referenceTypes,
        bool produceExecutable,
        bool produceDebuggingInformation)
    {
        this.logger = logger;
        
        this.module = module;
        this.targetFramework = targetFramework;
        this.cabiSpecificSymbols = cabiSpecificSymbols;
        this.referenceTypes = new(
            this.logger,
            referenceTypes.OfType<TypeDefinition>(),
            type => type.FullName);
        this.produceExecutable = produceExecutable;
        this.produceDebuggingInformation = produceDebuggingInformation;

        this.importantTypes.Add("System.Object", this.module.TypeSystem.Object);
        this.importantTypes.Add("System.Void", this.module.TypeSystem.Void);
        this.importantTypes.Add("System.Byte", this.module.TypeSystem.Byte);
        this.importantTypes.Add("System.SByte", this.module.TypeSystem.SByte);
        this.importantTypes.Add("System.Int16", this.module.TypeSystem.Int16);
        this.importantTypes.Add("System.UInt16", this.module.TypeSystem.UInt16);
        this.importantTypes.Add("System.Int32", this.module.TypeSystem.Int32);
        this.importantTypes.Add("System.UInt32", this.module.TypeSystem.UInt32);
        this.importantTypes.Add("System.Int64", this.module.TypeSystem.Int64);
        this.importantTypes.Add("System.UInt64", this.module.TypeSystem.UInt64);
        this.importantTypes.Add("System.Single", this.module.TypeSystem.Single);
        this.importantTypes.Add("System.Double", this.module.TypeSystem.Double);
        this.importantTypes.Add("System.Boolean", this.module.TypeSystem.Boolean);
        this.importantTypes.Add("System.String", this.module.TypeSystem.String);
        this.importantTypes.Add("System.IntPtr", this.module.TypeSystem.IntPtr);
        this.importantTypes.Add("System.UIntPtr", this.module.TypeSystem.UIntPtr);
        this.importantTypes.Add("System.TypedReference", this.module.TypeSystem.TypedReference);

        this.systemValueTypeType = new(() =>
            this.UnsafeGetType("System.ValueType"));
        this.systemEnumType = new(() =>
            this.UnsafeGetType("System.Enum"));
        this.indexOutOfRangeCtor = new(() =>
            this.UnsafeGetMethod("System.IndexOutOfRangeException..ctor", new string[0]));

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
        this.cabiDataTypeInitializer = CreateTypeInitializer();

        this.currentFile = unknown;
        this.fileScopedType = this.CreateFileScopedType(
             CecilUtilities.SanitizeFileNameToMemberName("unknown.s"));
        this.fileScopedTypeInitializer = this.CreateTypeInitializer();
    }

    /////////////////////////////////////////////////////////////////////

    private TypeDefinition CreateFileScopedType(string typeName) => new(
        "",
        typeName,
        TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed |
        TypeAttributes.BeforeFieldInit | TypeAttributes.Class,
        this.module.TypeSystem.Object);

    private MethodDefinition CreateTypeInitializer() => new(
        "..cctor",
        MethodAttributes.Private | MethodAttributes.Static |
        MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
        this.module.TypeSystem.Void);

    private void BeginNewFileScope(string sourcePathDebuggerHint)
    {
        // Add latest file scoped type.
        if ((this.fileScopedType.Methods.Count >= 1 ||
            this.fileScopedType.Fields.Count >= 1 ||
            this.fileScopedType.NestedTypes.Count >= 1) &&
            !this.module.Types.Contains(this.fileScopedType))
        {
            // Schedule checking for type initializer when all initializer was applied.
            var capturedFileScopedType = this.fileScopedType;
            var capturedFileScopedTypeInitializer = this.fileScopedTypeInitializer;
            this.delayedCheckAfterLookingupActions.Add(() =>
            {
                // Add type initializer when body was appended.
                if (capturedFileScopedTypeInitializer.Body is { } body &&
                    body.Instructions is { } instructions &&
                    instructions.Count >= 1)
                {
                    instructions.Add(Instruction.Create(OpCodes.Ret));
                    body.InitLocals = false;
                    capturedFileScopedType.Methods.Add(capturedFileScopedTypeInitializer);
                }
            });

            this.module.Types.Add(this.fileScopedType);
        }

        // Lookup exist type or create new type.
        var typeName = CecilUtilities.SanitizeFileNameToMemberName(sourcePathDebuggerHint);
        if (this.module.Types.FirstOrDefault(type => type.Name == typeName) is { } type)
        {
            this.fileScopedType = type;
            if (this.fileScopedType.Methods.
                FirstOrDefault(m => m.IsConstructor && m.IsStatic) is { } cctor)
            {
                this.fileScopedTypeInitializer = cctor;
            }
        }
        else
        {
            this.fileScopedType = this.CreateFileScopedType(typeName);
            this.fileScopedTypeInitializer = this.CreateTypeInitializer();
        }
    }

    public void BeginNewCilSourceCode(
        string? basePath,
        string sourcePathDebuggerHint)
    {
        this.BeginNewFileScope(sourcePathDebuggerHint);

        this.currentFile = new(
            basePath,
            sourcePathDebuggerHint,
            DocumentLanguage.Cil);
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
        new($"<placeholder_method>_${this.placeholderIndex++}",
            MethodAttributes.Private | MethodAttributes.Abstract,
            this.UnsafeGetType("System.Void"));

    private FieldDefinition CreateDummyField() =>
        new($"<placeholder_field>_${this.placeholderIndex++}",
            FieldAttributes.Private | FieldAttributes.InitOnly,
            this.UnsafeGetType("System.Int32"));

    private TypeDefinition CreateDummyType() =>
        new("", $"<placeholder_type>_${this.placeholderIndex++}",
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

    private TypeReference UnsafeGetType(
        string typeName)
    {
        if (!this.TryGetType(typeName, out var type, null))
        {
            this.caughtError = true;
            this.logger.Error($"Could not find for important type: {typeName}");

            type = this.CreateDummyType();
        }
        return type;
    }

    private MethodReference UnsafeGetMethod(
        string methodName, string[] parameterTypeNames)
    {
        if (!this.TryGetMethod(methodName, parameterTypeNames, out var method, null))
        {
            this.caughtError = true;
            this.logger.Error($"Could not find for important method: {methodName}");

            method = this.CreateDummyMethod();
        }
        return method;
    }

    /////////////////////////////////////////////////////////////////////

    private bool TryGetType(
        string name,
        out TypeReference type) =>
        this.TryGetType(name, out type, this.fileScopedType);

    private bool TryGetType(
        string name,
        out TypeReference type,
        TypeDefinition? fileScopedType)
    {
        switch (name[name.Length - 1])
        {
            case '*':
                if (this.TryGetType(
                    name.Substring(0, name.Length - 1),
                    out var preType1,
                    fileScopedType))
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
                if (this.TryGetType(
                    name.Substring(0, name.Length - 1),
                    out var preType2,
                    fileScopedType))
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
                    this.TryGetType(
                        name.Substring(0, startBracketIndex),
                        out var elementType,
                        fileScopedType))
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
                        if (Utilities.TryParseInt32(
                            name.Substring(startBracketIndex + 1, name.Length - startBracketIndex - 2),
                            out var length) &&
                            length < 0)
                        {
                            type = null!;
                            return false;
                        }
                        else
                        {
                            // "aaa_len10"
                            type = this.GetValueArrayType(elementType, length);
                            return true;
                        }
                    }
                }
                else
                {
                    type = null!;
                    return false;
                }
            default:
                // Use current scoped type when not given.
                fileScopedType ??= this.fileScopedType;

                // IMPORTANT ORDER:
                //   Will lookup before this module, because the types redefinition by C headers
                //   each assembly (by generating chibias).
                //   Always we use first finding type, silently ignored when multiple declarations.
                if (fileScopedType?.NestedTypes.FirstOrDefault(type =>
                    type.Name == name) is { } td2)
                {
                    type = td2;
                    return true;
                }
                else if (this.module.Types.FirstOrDefault(type =>
                    (type.Namespace == "C.type" && type.Name == name) ||
                    // FullName is needed because the value array type name is not CABI.
                    (type.FullName == name)) is { } td3)
                {
                    type = td3;
                    return true;
                }
                else if (this.cabiSpecificSymbols.TryGetMember<TypeReference>(name, out var tr1))
                {
                    type = this.Import(tr1);
                    return true;
                }
                else if (this.importantTypes.TryGetValue(name, out type!))
                {
                    return true;
                }
                else if (CecilUtilities.TryLookupOriginTypeName(name, out var originTypeName))
                {
                    return this.TryGetType(
                        originTypeName,
                        out type,
                        fileScopedType);
                }
                else if (this.referenceTypes.TryGetMember(name, out var td4))
                {
                    type = this.Import(td4);
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
        string name,
        string[] parameterTypeNames,
        out MethodReference method) =>
        this.TryGetMethod(
            name, parameterTypeNames, out method, this.fileScopedType);

    private bool TryGetMethod(
        string name,
        string[] parameterTypeNames,
        out MethodReference method,
        TypeDefinition? fileScopedType)
    {
        var methodNameIndex = name.LastIndexOf('.');
        var methodName = name.Substring(methodNameIndex + 1);

        if (methodName == "ctor" || methodName == "cctor")
        {
            methodName = "." + methodName;
            methodNameIndex--;
        }

        // CABI specific case: No need to check any parameters.
        if (methodNameIndex <= 0 &&
            parameterTypeNames.Length == 0)
        {
            if (fileScopedType?.Methods.
                FirstOrDefault(method => method.Name == methodName) is { } m2)
            {
                method = m2;
                return true;
            }
            else if (this.cabiTextType.Methods.
                FirstOrDefault(method => method.Name == methodName) is { } m3)
            {
                method = m3;
                return true;
            }
            else if (this.cabiSpecificSymbols.TryGetMember<MethodReference>(methodName, out var m))
            {
                method = this.Import(m);
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
            Select(parameterTypeName => this.TryGetType(
                parameterTypeName,
                out var type,
                fileScopedType) ?
                    type.FullName : string.Empty).
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
                method.Parameters.Select(p => p.ParameterType.FullName))) is { } m4)
        {
            method = this.Import(m4);
            return true;
        }
        else
        {
            method = null!;
            return false;
        }
    }

    private bool TryGetField(
        string name,
        out FieldReference field) =>
        this.TryGetField(name, out field, this.fileScopedType);

    private bool TryGetField(
        string name,
        out FieldReference field,
        TypeDefinition? fileScopedType)
    {
        var fieldNameIndex = name.LastIndexOf('.');
        var fieldName = name.Substring(fieldNameIndex + 1);
        if (fieldNameIndex <= 0)
        {
            if (fileScopedType?.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f2)
            {
                field = f2;
                return true;
            }
            else if (this.cabiDataType.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f3)
            {
                field = f3;
                return true;
            }
            else if (this.cabiSpecificSymbols.TryGetMember<FieldReference>(name, out var f))
            {
                field = this.Import(f);
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
        Action<TypeReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetType(
                typeName,
                out var type,
                capturedFileScopedType))
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
    }

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
        Action<TypeReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetType(
                typeName,
                out var type,
                capturedFileScopedType))
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
    }

    private void DelayLookingUpField(
        string fieldName,
        Token fieldNameToken,
        Action<FieldReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(
                fieldName,
                out var field,
                capturedFileScopedType))
            {
                action(field);
            }
            else
            {
                this.OutputError(
                    fieldNameToken,
                    $"Could not find field: {fieldName}");
            }
        });
    }

    private void DelayLookingUpField(
        Token fieldNameToken,
        Action<FieldReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(
                fieldNameToken.Text,
                out var field,
                capturedFileScopedType))
            {
                action(field);
            }
            else
            {
                this.OutputError(
                    fieldNameToken,
                    $"Could not find field: {fieldNameToken.Text}");
            }
        });
    }

    private void DelayLookingUpField(
        string fieldName,
        Location location,
        Action<FieldReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(
                fieldName,
                out var field,
                capturedFileScopedType))
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
    }

    private void DelayLookingUpMethod(
        Token methodNameToken,
        string[] parameterTypeNames,
        Action<MethodReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMethod(
                methodNameToken.Text,
                parameterTypeNames,
                out var method,
                capturedFileScopedType))
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
    }

    private void DelayLookingUpMethod(
        string methodName,
        string[] parameterTypeNames,
        Location location,
        Action<MethodReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMethod(
                methodName,
                parameterTypeNames,
                out var method,
                capturedFileScopedType))
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
    }

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
                         CecilUtilities.TryParseOpCode(token0.Text, out var opCode):
                    this.ParseInstruction(opCode, tokens);
                    break;
                // Is it an enumeration member?
                case TokenTypes.Identity
                    when this.enumerationType != null:
                    this.ParseEnumerationMember(tokens);
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
            Debug.Assert(this.enumerationType == null);

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
        else if (this.enumerationType != null)
        {
            Debug.Assert(this.method == null);
            Debug.Assert(this.instructions == null);
            Debug.Assert(this.body == null);
            Debug.Assert(this.structureType == null);

            if (this.checkingMemberIndex >= 0 &&
                this.checkingMemberIndex < this.enumerationType.Fields.
                Count(f => f.IsPublic && f.IsStatic && f.IsLiteral))
            {
                this.caughtError = true;
                this.logger.Error(
                    $"Enumeration member difference exists before declared type: {this.enumerationType.Name}");
            }

            this.enumerationType = null;
            this.checkingMemberIndex = -1;

            this.enumerationUnderlyingType = null;
            this.enumerationManipulator = null;
        }
        else if (this.structureType != null)
        {
            Debug.Assert(this.method == null);
            Debug.Assert(this.instructions == null);
            Debug.Assert(this.body == null);
            Debug.Assert(this.enumerationType == null);
            
            Debug.Assert(this.delayedLookupBranchTargetActions.Count == 0);
            Debug.Assert(this.labelTargets.Count == 0);
            Debug.Assert(this.willApplyLabelingNames.Count == 0);

            if (this.checkingMemberIndex >= 0 &&
                this.checkingMemberIndex < this.structureType.Fields.Count)
            {
                this.caughtError = true;
                this.logger.Error(
                    $"Structure member difference exists before declared type: {this.structureType.Name}");
            }

            this.structureType = null;
            this.checkingMemberIndex = -1;
        }
    }

    public bool Finish(bool applyOptimization)
    {
        this.FinishCurrentState();

        if (!this.caughtError)
        {
            // Clean up for file scope type.
            this.BeginNewFileScope("unknown.s");

            // Add text type when exist methods.
            if (this.cabiTextType.Methods.Count >= 1)
            {
                this.module.Types.Add(this.cabiTextType);
            }

            // Add data type when exist methods.
            if (this.cabiDataType.Fields.Count >= 1)
            {
                this.module.Types.Add(this.cabiDataType);
            }

            // Fire local member lookup.
            foreach (var action in this.delayedLookupLocalMemberActions)
            {
                action();
            }

            // Fire lookup checker.
            foreach (var action in this.delayedCheckAfterLookingupActions)
            {
                action();
            }

            // Add type initializer when body was appended.
            if (this.cabiDataTypeInitializer.Body is { } cabiDataTypeInitializerBody &&
                cabiDataTypeInitializerBody.Instructions is { } cabiDataTypeInitializerInstructions &&
                cabiDataTypeInitializerInstructions.Count >= 1)
            {
                cabiDataTypeInitializerInstructions.Add(Instruction.Create(OpCodes.Ret));
                cabiDataTypeInitializerBody.InitLocals = false;
                this.cabiDataType.Methods.Add(this.cabiDataTypeInitializer);
            }

            // Apply TFA if could be imported.
            if (this.TryGetMethod(
                "System.Runtime.Versioning.TargetFrameworkAttribute..ctor",
                new[] { "System.String" },
                out var tfctor))
            {
                var tfa = new CustomAttribute(tfctor);
                tfa.ConstructorArguments.Add(new(
                    this.UnsafeGetType("System.String"),
                    this.targetFramework.ToString()));
                this.module.Assembly.CustomAttributes.Add(tfa);
            }

            // main entry point lookup.
            if (this.produceExecutable)
            {
                if (this.module.Types.
                    Where(type => type.IsAbstract && type.IsSealed).
                    SelectMany(type => type.Methods).
                    FirstOrDefault(m => m.IsStatic && m.Parameters.Count == 0 &&
                    m.Name == "main") is { } mainMethod)                        
                {
                    this.module.EntryPoint = mainMethod;
                }
                else
                {
                    this.caughtError = true;
                    this.logger.Error($"{this.currentFile.RelativePath}(1,1): Could not find main entry point.");
                }
            }

            // (Completed all CIL implementations in this place.)

            ///////////////////////////////////////////////

            var documents = new Dictionary<string, Document>();
            foreach (var method in this.module.Types.
                SelectMany(type => type.Methods).
                Where(method =>
                    !method.IsAbstract &&
                    method.HasBody &&
                    method.Body.Instructions.Count >= 1))
            {
                var body = method.Body;

                // Apply optimization.
                if (applyOptimization)
                {
                    body.Optimize();
                }

                // After optimization, the instructions maybe changed absolute layouts,
                // so we could set debugging information scope after that.
                if (this.produceDebuggingInformation)
                {
                    method.DebugInformation.Scope = new ScopeDebugInformation(
                        body.Instructions.First(),
                        body.Instructions.Last());

                    // Will make sequence points:
                    foreach (var instruction in body.Instructions)
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

                            sequencePoint.StartLine = (int)(location.StartLine + 1);
                            sequencePoint.StartColumn = (int)(location.StartColumn + 1);
                            sequencePoint.EndLine = (int)(location.EndLine + 1);
                            sequencePoint.EndColumn = (int)(location.EndColumn + 1);

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

        this.delayedLookupLocalMemberActions.Clear();
        this.delayedCheckAfterLookingupActions.Clear();
        this.files.Clear();
        this.locationByInstructions.Clear();
        this.variableDebugInformationLists.Clear();

        this.isProducedOriginalSourceCodeLocation = true;
        this.currentFile = unknown;
        this.queuedLocation = null;
        this.lastLocation = null;

        this.referenceTypes.Finish();

        var finished = !this.caughtError;
        this.caughtError = false;

        return finished;
    }
}
