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

namespace chibias.Internal;

partial class Parser
{
    private MethodDefinition SetupMethodDefinition(
        string functionName,
        TypeReference returnType,
        ParameterDefinition[] parameters,
        MethodAttributes attribute,
        bool varargs)
    {
        this.method = new MethodDefinition(
            functionName,
            attribute | MethodAttributes.Static,
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
            return;
        }

        if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
            return;
        }

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
                continue;
            }

            if (index == (tokens.Length - 1) &&
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

                continue;
            }

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

        method = this.SetupMethodDefinition(
            functionName,
            returnType,
            parameters.ToArray(),
            scopeDescriptor switch
            {
                ScopeDescriptors.Public => MethodAttributes.Public | MethodAttributes.Static,
                _ => MethodAttributes.Assembly | MethodAttributes.Static,
            },
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

    /////////////////////////////////////////////////////////////////////

    private void ParseInitializerDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 2)
        {
            this.OutputError(tokens.Last(), $"Missing directive operands.");
            return;
        }

        if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
            return;
        }

        var method = this.SetupMethodDefinition(
            $"initializer_${this.initializerIndex++}",
            this.UnsafeGetType("System.Void"),
            Utilities.Empty<ParameterDefinition>(),
            MethodAttributes.Private,
            false);

        switch (scopeDescriptor)
        {
            case ScopeDescriptors.Public:
            case ScopeDescriptors.Internal:
                this.cabiDataType.Methods.Add(method);
                this.cabiDataTypeInitializer.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Call, method));
                break;
            default:
                this.fileScopedType.Methods.Add(method);
                this.fileScopedTypeInitializer.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Call, method));
                break;
        }
    }

    /////////////////////////////////////////////////////////////////////

    private static bool TryAddInitializer(
        Mono.Collections.Generic.Collection<Instruction> instruction,
        FieldDefinition storeToField,
        MemberReference pointToMember,
        int offset)
    {
        if (pointToMember is MethodReference pointToMethod)
        {
            switch (storeToField.FieldType.FullName)
            {
                case "System.IntPtr":
                    instruction.Add(
                        Instruction.Create(OpCodes.Ldftn, pointToMethod));
                    if (offset != 0)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldc_I4, offset));
                        instruction.Add(
                            Instruction.Create(OpCodes.Conv_I));
                        instruction.Add(
                            Instruction.Create(OpCodes.Add));
                    }
                    instruction.Add(
                        Instruction.Create(OpCodes.Conv_I));
                    instruction.Add(
                        Instruction.Create(OpCodes.Stsfld, storeToField));
                    return true;
                case "System.UIntPtr":
                    instruction.Add(
                        Instruction.Create(OpCodes.Ldftn, pointToMethod));
                    if (offset != 0)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldc_I4, offset));
                        instruction.Add(
                            Instruction.Create(OpCodes.Conv_I));
                        instruction.Add(
                            Instruction.Create(OpCodes.Add));
                    }
                    instruction.Add(
                        Instruction.Create(OpCodes.Conv_U));
                    instruction.Add(
                        Instruction.Create(OpCodes.Stsfld, storeToField));
                    return true;
                case "System.Void*":
                    instruction.Add(
                        Instruction.Create(OpCodes.Ldftn, pointToMethod));
                    if (offset != 0)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldc_I4, offset));
                        instruction.Add(
                            Instruction.Create(OpCodes.Conv_I));
                        instruction.Add(
                            Instruction.Create(OpCodes.Add));
                    }
                    instruction.Add(
                        Instruction.Create(OpCodes.Stsfld, storeToField));
                    return true;
                default:
                    // Check whatever equality for function pointer type.
                    if (pointToMethod.TryMakeFunctionPointerType(
                        out var pointToFieldPointerType) &&
                        storeToField.FieldType.FullName == pointToFieldPointerType.FullName)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldftn, pointToMethod));
                        if (offset != 0)
                        {
                            instruction.Add(
                                Instruction.Create(OpCodes.Ldc_I4, offset));
                            instruction.Add(
                                Instruction.Create(OpCodes.Conv_I));
                            instruction.Add(
                                Instruction.Create(OpCodes.Add));
                        }
                        instruction.Add(
                            Instruction.Create(OpCodes.Stsfld, storeToField));
                        return true;
                    }
                    break;
            }
        }
        else if (pointToMember is FieldReference pointToField)
        {
            switch (storeToField.FieldType.FullName)
            {
                case "System.IntPtr":
                    instruction.Add(
                        Instruction.Create(OpCodes.Ldsflda, pointToField));
                    if (offset != 0)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldc_I4, offset));
                        instruction.Add(
                            Instruction.Create(OpCodes.Conv_I));
                        instruction.Add(
                            Instruction.Create(OpCodes.Add));
                    }
                    instruction.Add(
                        Instruction.Create(OpCodes.Conv_I));
                    instruction.Add(
                        Instruction.Create(OpCodes.Stsfld, storeToField));
                    return true;
                case "System.UIntPtr":
                    instruction.Add(
                        Instruction.Create(OpCodes.Ldsflda, pointToField));
                    if (offset != 0)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldc_I4, offset));
                        instruction.Add(
                            Instruction.Create(OpCodes.Conv_I));
                        instruction.Add(
                            Instruction.Create(OpCodes.Add));
                    }
                    instruction.Add(
                        Instruction.Create(OpCodes.Conv_U));
                    instruction.Add(
                        Instruction.Create(OpCodes.Stsfld, storeToField));
                    return true;
                case "System.Void*":
                    instruction.Add(
                        Instruction.Create(OpCodes.Ldsflda, pointToField));
                    if (offset != 0)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldc_I4, offset));
                        instruction.Add(
                            Instruction.Create(OpCodes.Conv_I));
                        instruction.Add(
                            Instruction.Create(OpCodes.Add));
                    }
                    instruction.Add(
                        Instruction.Create(OpCodes.Stsfld, storeToField));
                    return true;
                default:
                    // Check whatever equality for pointer type.
                    var pointToFieldPointerType = new PointerType(pointToField.FieldType);
                    if (storeToField.FieldType.FullName == pointToFieldPointerType.FullName)
                    {
                        instruction.Add(
                            Instruction.Create(OpCodes.Ldsflda, pointToField));
                        if (offset != 0)
                        {
                            instruction.Add(
                                Instruction.Create(OpCodes.Ldc_I4, offset));
                            instruction.Add(
                                Instruction.Create(OpCodes.Conv_I));
                            instruction.Add(
                                Instruction.Create(OpCodes.Add));
                        }
                        instruction.Add(
                            Instruction.Create(OpCodes.Stsfld, storeToField));
                        return true;
                    }
                    break;
            }
        }

        return false;
    }

    private void ParseGlobalDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 4)
        {
            this.OutputError(directive, $"Missing global variable operand.");
            return;
        }

        if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
            return;
        }

        byte[]? data;
        Token? pointToMemberNameToken;

        // Pointing to variable.
        if (tokens.Length == 5 &&
            tokens.ElementAtOrDefault(4) is { } firstDataToken &&
            firstDataToken.Text.StartsWith("&") &&
            firstDataToken.Text.Length >= 2)
        {
            data = null;
            pointToMemberNameToken = firstDataToken;
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
            pointToMemberNameToken = null;
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
            Debug.Assert(pointToMemberNameToken == null);

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

        if (pointToMemberNameToken == null)
        {
            return;
        }

        Debug.Assert(data == null);

        var pointToMemberName = pointToMemberNameToken.Text.Substring(1);
        var offset = 0;
        if (pointToMemberName.IndexOf('+') is { } plusIndex &&
            plusIndex >= 0)
        {
            if (!Utilities.TryParseInt32(
                pointToMemberName.Substring(plusIndex + 1), out offset))
            {
                this.OutputError(
                    pointToMemberNameToken,
                    $"Invalid offset: {pointToMemberName}");
                return;
            }

            pointToMemberName = pointToMemberName.Substring(0, plusIndex);
        }
        else if (pointToMemberName.IndexOf('-') is { } minusIndex &&
            minusIndex >= 0)
        {
            if (!Utilities.TryParseInt32(
                pointToMemberName.Substring(minusIndex + 1), out offset))
            {
                this.OutputError(
                    pointToMemberNameToken,
                    $"Invalid offset: {pointToMemberName}");
                return;
            }

            offset = 0 - offset;
            pointToMemberName = pointToMemberName.Substring(0, minusIndex);
        }

        var capturedInstructions = scopeDescriptor switch
        {
            ScopeDescriptors.Public => this.cabiDataTypeInitializer.Body.Instructions,
            ScopeDescriptors.Internal => this.cabiDataTypeInitializer.Body.Instructions,
            _ => this.fileScopedTypeInitializer.Body.Instructions,
        };

        if (!this.TryGetMember(
            pointToMemberName,
            Utilities.Empty<string>(),
            out var ptm))
        {
            var capturedField = field;
            var capturedPointToVariableNameToken = pointToMemberNameToken;
            this.DelayLookingUpMember(
                pointToMemberName,
                pointToMemberNameToken,
                Utilities.Empty<string>(),
                pointToMember =>
                {
                    if (!TryAddInitializer(
                        capturedInstructions, capturedField, pointToMember, offset))
                    {
                        this.OutputError(
                            capturedPointToVariableNameToken,
                            $"Member type is not compatible pointer type: {pointToMember.FullName}");
                    }
                });
        }
        else if (!TryAddInitializer(capturedInstructions, field, ptm, offset))
        {
            this.OutputError(
                pointToMemberNameToken,
                $"Member type is not compatible pointer type: {ptm.FullName}");
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
            return;
        }

        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing local variable operand.");
            return;
        }

        if (tokens.Length > 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
            return;
        }

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
            return;
        }
        
        if (tokens.Length > 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
            return;
        }

        if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
            return;
        }

        var typeAttributes = scopeDescriptor switch
        {
            ScopeDescriptors.Public => TypeAttributes.Public | TypeAttributes.Sealed,
            ScopeDescriptors.Internal => TypeAttributes.NotPublic | TypeAttributes.Sealed,
            _ => TypeAttributes.NestedAssembly | TypeAttributes.Sealed,
        };
        var valueFieldAttributes =
            FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName;

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
            return;
        }

        var underlyingType = this.UnsafeGetType(underlyingTypeName);

        var enumerationTypeNameToken = tokens[3];
        var enumerationTypeName = enumerationTypeNameToken.Text;

        if (this.TryGetType(enumerationTypeName, out var etref))
        {
            // Checks equality
            var et = etref.Resolve();
            if ((et.Attributes & typeAttributes) != typeAttributes)
            {
                this.OutputError(
                    enumerationTypeNameToken,
                    $"Type attribute difference exists before declared type: {et.Attributes}");
                return;
            }

            if (et.BaseType.FullName != "System.Enum")
            {
                this.OutputError(
                    enumerationTypeNameToken,
                    $"Base type difference exists before declared type: {et.BaseType.FullName}");
                return;
            }

            if (et.GetEnumUnderlyingType() is { } ut &&
                ut.FullName != underlyingType.FullName)
            {
                this.OutputError(
                    enumerationTypeNameToken,
                    $"Enumeration underlying type difference exists before declared type: {ut.FullName}");
                return;
            }

            if (!(et.Fields.FirstOrDefault(f => f.Name == "value__") is { } evf))
            {
                this.OutputError(
                    enumerationTypeNameToken,
                    "Enumeration value field type is not declared.");
                return;
            }

            if (evf.FieldType.FullName != underlyingType.FullName)
            {
                this.OutputError(
                    enumerationTypeNameToken,
                    $"Enumeration value field type difference exists before declared type: {evf.FieldType.FullName}");
                return;
            }

            if ((evf.Attributes & valueFieldAttributes) != valueFieldAttributes)
            {
                this.OutputError(
                    enumerationTypeNameToken,
                    $"Enumeration value field type attribute difference exists before declared type: {et.Attributes}");
                return;
            }

            this.enumerationType = et;
            this.checkingMemberIndex = 0;

            this.enumerationUnderlyingType = underlyingType;
            this.enumerationManipulator =
                EnumerationMemberValueManipulator.GetInstance(underlyingType);

            return;
        }

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

        this.enumerationUnderlyingType =
            underlyingType;
        this.enumerationManipulator =
            EnumerationMemberValueManipulator.GetInstance(underlyingType);
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
            return;
        }

        if (tokens.Length > 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
            return;
        }

        if (!CecilUtilities.TryLookupScopeDescriptorName(
            tokens[1].Text,
            out var scopeDescriptor))
        {
            this.OutputError(
                tokens[1],
                $"Invalid scope descriptor: {tokens[1].Text}");
            return;
        }

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
                    return;
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
            var st = stref.Resolve();
            if ((st.Attributes & typeAttributes) != typeAttributes)
            {
                this.OutputError(
                    structureTypeNameToken,
                    $"Type attribute difference exists before declared type: {st.Attributes}");
                return;
            }

            if (packSize is { } ps2 &&
                st.PackingSize != ps2)
            {
                this.OutputError(
                    aligningToken!,
                    $"Packing size difference exists before declared type: {st.PackingSize}");
                return;
            }

            if (packSize == null &&
                st.PackingSize >= 1)
            {
                this.OutputError(
                    aligningToken!,
                    $"Packing size difference exists before declared type: {st.PackingSize}");
                return;
            }

            this.structureType = st;
            this.checkingMemberIndex = 0;

            return;
        }

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

        if (packSize is { } ps3)
        {
            structureType.PackingSize = ps3;
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

    /////////////////////////////////////////////////////////////////////

    private void ParseFileDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing file operands.");
            return;
        }

        if (tokens.Length > 4)
        {
            this.OutputError(
                tokens[4],
                $"Too many operands.");
            return;
        }
        
        if (Utilities.TryParseEnum<DocumentLanguage>(tokens[3].Text, out var language))
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
            return;
        }
        
        if (tokens.Length < 6)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing location operand.");
            return;
        }
        
        if (tokens.Length > 6)
        {
            this.OutputError(
                tokens[6],
                $"Too many operands.");
            return;
        }
        
        if (!this.files.TryGetValue(tokens[1].Text, out var file))
        {
            this.OutputError(
                tokens[1],
                $"Unknown file ID.");
            return;
        }

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
            return;
        }

        if (this.produceDebuggingInformation)
        {
            var location = new Location(
                file, vs[0], vs[1], vs[2], vs[3]);
            this.queuedLocation = location;
            this.isProducedOriginalSourceCodeLocation = false;
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
            // Initializer directive:
            case "initializer":
                this.ParseInitializerDirective(directive, tokens);
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
