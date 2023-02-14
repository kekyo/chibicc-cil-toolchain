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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace chibias.Internal;

partial class Parser
{
    private MethodDefinition SetupMethodDefinition(
        string functionName,
        TypeReference returnType,
        ParameterDefinition[] parameters,
        ScopeDescriptors scopeDescriptor,
        bool varargs)
    {
        this.method = new MethodDefinition(
            functionName,
            scopeDescriptor switch
            {
                ScopeDescriptors.Public => MethodAttributes.Public | MethodAttributes.Static,
                _ => MethodAttributes.Assembly | MethodAttributes.Static,
            },
            this.Import(returnType));
        this.method.HasThis = false;

        foreach (var parameter in parameters)
        {
            this.method.Parameters.Add(parameter);
        }

        if (varargs)
        {
            this.method.CallingConvention = MethodCallingConvention.VarArg;
        }

        this.body = this.method.Body;
        this.body.InitLocals = false;   // Derived C behavior.

        this.instructions = this.body.Instructions;

        return this.method;
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseFunctionDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 4)
        {
            this.OutputError(tokens.Last(), $"Missing directive operands.");
        }
        else if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
        }
        else
        {
            var returnTypeNameToken = tokens[2];
            var returnTypeName = returnTypeNameToken.Text;
            var functionName = tokens[3].Text;

            MethodDefinition method = null!;
            if (!this.TryGetType(returnTypeName, out var returnType))
            {
                returnType = this.CreateDummyType();

                this.DelayLookingUpType(
                    returnTypeNameToken,
                    type => method.ReturnType = type);   // (captured)
            }

            var parameters = new List<ParameterDefinition>();
            var varargs = false;
            for (var index = 4; index < tokens.Length; index++)
            {
                var parameterToken = tokens[index];
                var parameterTokenText = parameterToken.Text;

                var splitted = parameterTokenText.Split(':');
                if (splitted.Length >= 3)
                {
                    this.OutputError(
                        parameterToken,
                        $"Invalid parameter: {parameterTokenText}");
                }
                else if (index == (tokens.Length - 1) &&
                    splitted.Last() == "...")
                {
                    if (splitted.Length == 2)
                    {
                        this.OutputError(
                            parameterToken,
                            $"Could not apply any types at the variable argument descriptor: {parameterTokenText}");
                    }
                    else
                    {
                        varargs = true;
                    }
                }
                else
                {
                    var parameterTypeName = splitted.Last();

                    ParameterDefinition parameter = null!;
                    if (!this.TryGetType(parameterTypeName, out var parameterType))
                    {
                        parameterType = CreateDummyType();

                        this.DelayLookingUpType(
                            parameterTypeName,
                            parameterToken,
                            type => parameter.ParameterType = type);
                    }

                    parameter = new(parameterType);

                    if (splitted.Length == 2)
                    {
                        parameter.Name = splitted[0];
                    }

                    parameters.Add(parameter);
                }
            }

            method = this.SetupMethodDefinition(
                functionName,
                returnType,
                parameters.ToArray(),
                scopeDescriptor,
                varargs);

            switch (scopeDescriptor)
            {
                case ScopeDescriptors.Public:
                case ScopeDescriptors.Internal:
                    this.cabiTextType.Methods.Add(method);
                    break;
                default:
                    this.fileScopedType.Methods.Add(method);
                    break;
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseGlobalDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 4)
        {
            this.OutputError(directive, $"Missing global variable operand.");
        }
        else if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
        }
        else
        {
            byte[]? data;
            Token? pointToVariableNameToken;

            // Pointing to variable.
            if (tokens.Length == 5 &&
                tokens.ElementAtOrDefault(4) is { } firstDataToken &&
                firstDataToken.Text.StartsWith("&") &&
                firstDataToken.Text.Length >= 2)
            {
                data = null;
                pointToVariableNameToken = firstDataToken;
            }
            else
            {
                data = tokens.Skip(4).
                    Select(token =>
                    {
                        if (Utilities.TryParseUInt8(token.Text, out var value))
                        {
                            return value;
                        }
                        else
                        {
                            this.OutputError(token, $"Invalid data operand.");
                            return (byte)0;
                        }
                    }).
                    ToArray();
                pointToVariableNameToken = null;
            }

            var globalTypeNameToken = tokens[2];
            var globalTypeName = globalTypeNameToken.Text;
            var globalName = tokens[3].Text;

            FieldDefinition field = null!;
            if (!this.TryGetType(globalTypeName, out var globalType))
            {
                globalType = this.CreateDummyType();

                this.DelayLookingUpType(
                    globalTypeNameToken,
                    type => field.FieldType = type);   // (captured)
            }

            field = new FieldDefinition(
                globalName,
                scopeDescriptor switch
                {
                    ScopeDescriptors.Public => FieldAttributes.Public | FieldAttributes.Static,
                    _ => FieldAttributes.Assembly | FieldAttributes.Static,
                },
                globalType);
            if (data is { })
            {
                Debug.Assert(pointToVariableNameToken == null);

                field.InitialValue = data;
            }

            switch (scopeDescriptor)
            {
                case ScopeDescriptors.Public:
                case ScopeDescriptors.Internal:
                    this.cabiDataType.Fields.Add(field);
                    break;
                default:
                    this.fileScopedType.Fields.Add(field);
                    break;
            }

            if (pointToVariableNameToken is { })
            {
                Debug.Assert(data == null);

                var pointToVariableName = pointToVariableNameToken.Text.Substring(1);
                var capturedInstructions = scopeDescriptor switch
                {
                    ScopeDescriptors.Public => this.cabiDataTypeInitializer.Body.Instructions,
                    ScopeDescriptors.Internal => this.cabiDataTypeInitializer.Body.Instructions,
                    _ => this.fileScopedTypeInitializer.Body.Instructions,
                };

                static bool TryAddInitializer(
                    Mono.Collections.Generic.Collection<Instruction> instruction,
                    FieldDefinition storeToField,
                    FieldReference pointToField)
                {
                    switch (storeToField.FieldType.FullName)
                    {
                        case "System.IntPtr":
                            instruction.Add(
                                Instruction.Create(OpCodes.Ldsflda, pointToField));
                            instruction.Add(
                                Instruction.Create(OpCodes.Conv_I));
                            instruction.Add(
                                Instruction.Create(OpCodes.Stsfld, storeToField));
                            return true;
                        case "System.UIntPtr":
                            instruction.Add(
                                Instruction.Create(OpCodes.Ldsflda, pointToField));
                            instruction.Add(
                                Instruction.Create(OpCodes.Conv_U));
                            instruction.Add(
                                Instruction.Create(OpCodes.Stsfld, storeToField));
                            return true;
                        case "System.Void*":
                            instruction.Add(
                                Instruction.Create(OpCodes.Ldsflda, pointToField));
                            instruction.Add(
                                Instruction.Create(OpCodes.Stsfld, storeToField));
                            return true;
                        default:
                            var pointToFieldPointerType = new PointerType(pointToField.FieldType);
                            if (storeToField.FieldType.FullName == pointToFieldPointerType.FullName)
                            {
                                instruction.Add(
                                    Instruction.Create(OpCodes.Ldsflda, pointToField));
                                instruction.Add(
                                    Instruction.Create(OpCodes.Stsfld, storeToField));
                                return true;
                            }
                            break;
                    }

                    return false;
                }

                if (!this.TryGetField(pointToVariableName, out var ptf))
                {
                    var capturedField = field;
                    var capturedPointToVariableNameToken = pointToVariableNameToken;
                    this.DelayLookingUpField(
                        pointToVariableName,
                        pointToVariableNameToken,
                        pointToField =>
                        {
                            if (!TryAddInitializer(
                                capturedInstructions, capturedField, pointToField))
                            {
                                this.OutputError(
                                    capturedPointToVariableNameToken,
                                    $"Field type is not compatible pointer type: {pointToField.FullName}");
                            }
                        });
                }
                else if (!TryAddInitializer(
                    capturedInstructions, field, ptf))
                {
                    this.OutputError(
                        pointToVariableNameToken,
                        $"Field type is not compatible pointer type: {ptf.FullName}");
                }
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseLocalDirective(
        Token directive, Token[] tokens)
    {
        if (this.instructions == null)
        {
            this.OutputError(
                directive,
                $"Function directive is not defined.");
        }
        else if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing local variable operand.");
        }
        else if (tokens.Length > 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else
        {
            var localTypeName = tokens[1].Text;

            VariableDefinition variable = null!;
            if (!this.TryGetType(localTypeName, out var localType))
            {
                localType = this.CreateDummyType();

                this.DelayLookingUpType(
                    tokens[1],
                    type => variable.VariableType = type);   // (captured)
            }

            variable = new VariableDefinition(localType);
            this.body!.Variables.Add(variable);

            if (tokens.Length == 3)
            {
                var localName = tokens[2].Text;
                var variableDebugInformation = new VariableDebugInformation(
                    variable, localName);

                if (!this.variableDebugInformationLists.TryGetValue(
                    this.method!.Name,
                    out var list))
                {
                    list = new();
                    this.variableDebugInformationLists.Add(
                        this.method!.Name,
                        list);
                }

                list.Add(variableDebugInformation);
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseEnumerationDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing enumeration operand.");
        }
        else if (tokens.Length > 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
        }
        else
        {
            var typeAttributes = scopeDescriptor switch
            {
                ScopeDescriptors.Public => TypeAttributes.Public | TypeAttributes.Sealed,
                ScopeDescriptors.Internal => TypeAttributes.NotPublic | TypeAttributes.Sealed,
                _ => TypeAttributes.NestedAssembly | TypeAttributes.Sealed,
            };
            var valueFieldAttributes =
                FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName;

            var underlyingType = this.UnsafeGetType("System.Int32");

            var underlyingTypeNameToken = tokens[2];
            var underlyingTypeName = underlyingTypeNameToken.Text;

            if (CecilUtilities.TryLookupOriginTypeName(underlyingTypeName, out var originName))
            {
                underlyingTypeName = originName;
            }

            if (!CecilUtilities.IsEnumerationUnderlyingType(underlyingTypeName))
            {
                this.OutputError(
                    underlyingTypeNameToken,
                    $"Invalid enumeration underlying type: {underlyingTypeName}");
            }
            else
            {
                underlyingType = this.UnsafeGetType(underlyingTypeName);
            }

            var enumerationTypeNameToken = tokens[3];
            var enumerationTypeName = enumerationTypeNameToken.Text;

            if (this.TryGetType(enumerationTypeName, out var etref))
            {
                // Checks equality
                var enumerationType = etref.Resolve();
                if ((enumerationType.Attributes & typeAttributes) != typeAttributes)
                {
                    this.OutputError(
                        enumerationTypeNameToken,
                        $"Type attribute difference exists before declared type: {enumerationType.Attributes}");
                }
                else if (enumerationType.BaseType.FullName != "System.Enum")
                {
                    this.OutputError(
                        enumerationTypeNameToken,
                        $"Base type difference exists before declared type: {enumerationType.BaseType.FullName}");
                }
                else if (enumerationType.GetEnumUnderlyingType() is { } ut &&
                    ut.FullName != underlyingType.FullName)
                {
                    this.OutputError(
                        enumerationTypeNameToken,
                        $"Enumeration underlying type difference exists before declared type: {ut.FullName}");
                }
                else if (!(enumerationType.Fields.FirstOrDefault(f => f.Name == "value__") is { } enumerationValueField))
                {
                    this.OutputError(
                        enumerationTypeNameToken,
                        "Enumeration value field type is not declared.");
                }
                else if (enumerationValueField.FieldType.FullName != underlyingType.FullName)
                {
                    this.OutputError(
                        enumerationTypeNameToken,
                        $"Enumeration value field type difference exists before declared type: {enumerationValueField.FieldType.FullName}");
                }
                else if ((enumerationValueField.Attributes & valueFieldAttributes) != valueFieldAttributes)
                {
                    this.OutputError(
                        enumerationTypeNameToken,
                        $"Enumeration value field type attribute difference exists before declared type: {enumerationType.Attributes}");
                }

                this.enumerationType = enumerationType;
                this.checkingMemberIndex = 0;
            }
            else
            {
                var enumerationType = new TypeDefinition(
                    scopeDescriptor switch
                    {
                        ScopeDescriptors.Public => "C.type",
                        ScopeDescriptors.Internal => "C.type",
                        _ => "",
                    },
                    enumerationTypeName,
                    typeAttributes,
                    this.systemEnumType.Value);

                var enumerationValueField = new FieldDefinition(
                    "value__",
                    valueFieldAttributes,
                    underlyingType);
                enumerationType.Fields.Add(enumerationValueField);

                switch (scopeDescriptor)
                {
                    case ScopeDescriptors.Public:
                    case ScopeDescriptors.Internal:
                        this.module.Types.Add(enumerationType);
                        break;
                    case ScopeDescriptors.File:
                        this.fileScopedType.NestedTypes.Add(enumerationType);
                        break;
                }
                this.enumerationType = enumerationType;

                Debug.Assert(this.checkingMemberIndex == -1);
            }

            this.enumerationUnderlyingType =
                underlyingType;
            this.enumerationManipulator =
                EnumerationMemberValueManipulator.GetInstance(underlyingType);
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseStructureDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing structure operand.");
        }
        else if (tokens.Length > 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
        }
        else
        {
            var typeAttributes = scopeDescriptor switch
            {
                ScopeDescriptors.Public => TypeAttributes.Public | TypeAttributes.Sealed,
                ScopeDescriptors.Internal => TypeAttributes.NotPublic | TypeAttributes.Sealed,
                _ => TypeAttributes.NestedAssembly | TypeAttributes.Sealed,
            };

            short? packSize = null;
            var aligningToken = tokens.ElementAtOrDefault(3);

            if (aligningToken is { })
            {
                var aligning = aligningToken.Text;
                if (aligning == "explicit")
                {
                    typeAttributes |= TypeAttributes.ExplicitLayout;
                }
                else if (Utilities.TryParseInt16(aligning, out var ps))
                {
                    typeAttributes |= TypeAttributes.SequentialLayout;
                    if (ps >= 1)
                    {
                        packSize = ps;
                    }
                    else
                    {
                        this.OutputError(
                            aligningToken,
                            $"Invalid pack size: {aligning}");
                    }
                }
                else
                {
                    typeAttributes |= TypeAttributes.SequentialLayout;
                }
            }
            else
            {
                typeAttributes |= TypeAttributes.SequentialLayout;
            }

            var structureTypeNameToken = tokens[2];
            var structureTypeName = structureTypeNameToken.Text;

            if (this.TryGetType(structureTypeName, out var stref))
            {
                // Checks equality
                var structureType = stref.Resolve();
                if ((structureType.Attributes & typeAttributes) != typeAttributes)
                {
                    this.OutputError(
                        structureTypeNameToken,
                        $"Type attribute difference exists before declared type: {structureType.Attributes}");
                }
                else if (packSize is { } ps &&
                    structureType.PackingSize != ps)
                {
                    this.OutputError(
                        aligningToken!,
                        $"Packing size difference exists before declared type: {structureType.PackingSize}");
                }
                else if (packSize == null &&
                    structureType.PackingSize >= 1)
                {
                    this.OutputError(
                        aligningToken!,
                        $"Packing size difference exists before declared type: {structureType.PackingSize}");
                }

                this.structureType = structureType;
                this.checkingMemberIndex = 0;
            }
            else
            {
                var structureType = new TypeDefinition(
                    scopeDescriptor switch
                    {
                        ScopeDescriptors.Public => "C.type",
                        ScopeDescriptors.Internal => "C.type",
                        _ => "",
                    },
                    structureTypeName,
                    typeAttributes,
                    this.systemValueTypeType.Value);
                if (packSize is { } ps)
                {
                    structureType.PackingSize = ps;
                    structureType.ClassSize = 0;
                }

                switch (scopeDescriptor)
                {
                    case ScopeDescriptors.Public:
                    case ScopeDescriptors.Internal:
                        this.module.Types.Add(structureType);
                        break;
                    case ScopeDescriptors.File:
                        this.fileScopedType.NestedTypes.Add(structureType);
                        break;
                }
                this.structureType = structureType;

                Debug.Assert(this.checkingMemberIndex == -1);
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseFileDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing file operands.");
        }
        else if (tokens.Length > 4)
        {
            this.OutputError(
                tokens[4],
                $"Too many operands.");
        }
        else if (Utilities.TryParseEnum<DocumentLanguage>(tokens[3].Text, out var language))
        {
            if (this.produceDebuggingInformation)
            {
                // NOT Utilities.GetDirectoryPath()
                var file = new FileDescriptor(
                    Path.GetDirectoryName(tokens[2].Text),
                    Path.GetFileName(tokens[2].Text),
                    language);
                this.currentFile = file;
                this.files[tokens[1].Text] = file;
                this.queuedLocation = null;
                this.lastLocation = null;
                this.isProducedOriginalSourceCodeLocation = false;
            }
        }
        else
        {
            this.OutputError(
                tokens[3], $"Invalid language operand: {tokens[3].Text}");
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseLocationDirective(
        Token directive, Token[] tokens)
    {
        if (this.instructions == null)
        {
            this.OutputError(
                directive,
                $"Function directive is not defined.");
        }
        else if (tokens.Length < 6)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing location operand.");
        }
        else if (tokens.Length > 6)
        {
            this.OutputError(
                tokens[6],
                $"Too many operands.");
        }
        else if (!this.files.TryGetValue(tokens[1].Text, out var file))
        {
            this.OutputError(
                tokens[1],
                $"Unknown file ID.");
        }
        else
        {
            var vs = tokens.
                Skip(2).
                Collect(token =>
                    (Utilities.TryParseUInt32(token.Text, out var vi) && vi >= 0) ?
                        vi : default(uint?)).
                ToArray();
            if ((vs.Length != (tokens.Length - 2)) ||
                (vs[0] > vs[2]) ||
                (vs[1] >= vs[3]))
            {
                this.OutputError(
                    directive,
                    $"Invalid operand: {tokens[1].Text}");
            }
            else if (this.produceDebuggingInformation)
            {
                var location = new Location(
                    file, vs[0], vs[1], vs[2], vs[3]);
                this.queuedLocation = location;
                this.isProducedOriginalSourceCodeLocation = false;
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseDirective(Token directive, Token[] tokens)
    {
        switch (directive.Text)
        {
            // Function directive:
            case "function":
                this.ParseFunctionDirective(directive, tokens);
                break;
            // Global variable directive:
            case "global":
                this.ParseGlobalDirective(directive, tokens);
                break;
            // Local variable directive:
            case "local":
                this.ParseLocalDirective(directive, tokens);
                break;
            // Enumeration directive:
            case "enumeration":
                this.ParseEnumerationDirective(directive, tokens);
                break;
            // Structure directive:
            case "structure":
                this.ParseStructureDirective(directive, tokens);
                break;
            // File directive:
            case "file":
                this.ParseFileDirective(directive, tokens);
                break;
            // Location directive:
            case "location":
                this.ParseLocationDirective(directive, tokens);
                break;
            // Other, invalid assembler directive.
            default:
                this.OutputError(
                    directive,
                    $"Invalid directive: .{directive.Text}");
                break;
        }
    }
}
