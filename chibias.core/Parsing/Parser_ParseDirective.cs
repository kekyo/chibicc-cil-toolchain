/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace chibias.Parsing;

partial class Parser
{
    private MethodDefinition SetupMethodDefinition(
        string functionName,
        TypeReference returnType,
        ParameterDefinition[] parameters,
        MethodAttributes attribute)
    {
        this.method = new MethodDefinition(
            functionName,
            attribute,
            this.Import(returnType));
        this.method.HasThis = false;

        foreach (var parameter in parameters)
        {
            this.method.Parameters.Add(parameter);
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

        if (tokens.Length != 4)
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

        var functionSignatureToken = tokens[2];
        var functionSignature = functionSignatureToken.Text;
        var functionName = tokens[3].Text;

        if (!TypeParser.TryParse(functionSignature, out var rootTypeNode))
        {
            this.OutputError(
                this.GetCurrentLocation(functionSignatureToken),
                $"Invalid function signature {functionSignature}");
            return;
        }

        if (!(rootTypeNode is FunctionSignatureNode fsn))
        {
            this.OutputError(
                this.GetCurrentLocation(functionSignatureToken),
                $"Invalid function signature: {functionSignature}");
            return;
        }

        var parameters =
            fsn.Parameters.Select(p =>
            {
                var pd = new ParameterDefinition(this.CreateDummyType());
                pd.Name = p.ParameterName;
                return pd;
            }).
            ToArray();

        var method = this.SetupMethodDefinition(
            functionName,
            this.CreateDummyType(),
            parameters,
            scopeDescriptor switch
            {
                ScopeDescriptors.Public => MethodAttributes.Public | MethodAttributes.Static,
                ScopeDescriptors.File => MethodAttributes.Public | MethodAttributes.Static,
                _ => MethodAttributes.Assembly | MethodAttributes.Static,
            });

        method.CallingConvention = fsn.CallingConvention;

        this.DelayLookingUpType(
            fsn.ReturnType,
            functionSignatureToken,
            LookupTargets.All,
            type =>
            {
                method.ReturnType = type;

                // Special case: Force 1 byte footprint on boolean type.
                if (type.FullName == "System.Boolean")
                {
                    method.MethodReturnType.MarshalInfo = new(NativeType.U1);
                }
                else if (type.FullName == "System.Char")
                {
                    method.MethodReturnType.MarshalInfo = new(NativeType.U2);
                }
            });

        for (var index = 0; index < fsn.Parameters.Length; index++)
        {
            var (parameterTypeNode, parameterName) = fsn.Parameters[index];

            var capturedParameter = method.Parameters[index];
            this.DelayLookingUpType(
                parameterTypeNode,
                functionSignatureToken,
                LookupTargets.All,
                type =>
                {
                    capturedParameter.ParameterType = type;

                    if (!string.IsNullOrWhiteSpace(parameterName))
                    {
                        capturedParameter.Name = parameterName;
                    }

                    // Special case: Force 1 byte footprint on boolean type.
                    if (type.FullName == "System.Boolean")
                    {
                        capturedParameter.MarshalInfo = new(NativeType.U1);
                    }
                    else if (type.FullName == "System.Char")
                    {
                        capturedParameter.MarshalInfo = new(NativeType.U2);
                    }
                });
        }

        switch (scopeDescriptor)
        {
            case ScopeDescriptors.Public:
            case ScopeDescriptors.Internal:
                this.cabiTextType.Methods.Add(method);
                break;
            case ScopeDescriptors.File:
                this.fileScopedType.Methods.Add(method);
                break;
            case ScopeDescriptors._Module_:
                this.module.Types.First(t => t.Name == "<Module>").Methods.Add(method);
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
            MethodAttributes.Private | MethodAttributes.Static);

        switch (scopeDescriptor)
        {
            case ScopeDescriptors.Public:
            case ScopeDescriptors.Internal:
                this.cabiDataType.Methods.Add(method);
                this.cabiDataTypeInitializer.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Call, method));
                break;
            case ScopeDescriptors.File:
                this.fileScopedType.Methods.Add(method);
                this.fileScopedTypeInitializer.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Call, method));
                break;
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
        if (tokens.Length >= 5)
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
        }
        else
        {
            data = null;
        }

        var globalTypeNameToken = tokens[2];
        var globalName = tokens[3].Text;

        var fa = scopeDescriptor switch
        {
            ScopeDescriptors.Public => FieldAttributes.Public | FieldAttributes.Static,
            ScopeDescriptors.File => FieldAttributes.Public | FieldAttributes.Static,
            _ => FieldAttributes.Assembly | FieldAttributes.Static,
        };

        if (this.TryGetField(
            globalName,
            out var f,
            this.fileScopedType,
            LookupTargets.All))
        {
            this.DelayLookingUpType(
                globalTypeNameToken,
                globalTypeNameToken,
                LookupTargets.All,
                type =>
                {
                    if (type.FullName != f.FieldType.FullName)
                    {
                        this.OutputError(
                            tokens[1],
                            $"Mismatched previous field type: {globalName}");
                    }
                });
            
            var previousField = f.Resolve();
            if (previousField.Attributes != fa)
            {
                this.OutputError(
                    tokens[1],
                    $"Mismatched previous field attribute: {globalName}");
            }

            if (data != null)
            {
                if (!previousField.InitialValue.SequenceEqual(data) || !previousField.IsInitOnly)
                {
                    this.OutputError(
                        tokens[3],
                        $"Mismatched previous field declaration: {globalName}");
                }
            }
            return;
        }

        var field = new FieldDefinition(
            globalName,
            fa,
            this.CreateDummyType());

        if (data != null)
        {
            field.InitialValue = data;
            field.IsInitOnly = true;
        }

        this.DelayLookingUpType(
            globalTypeNameToken,
            globalTypeNameToken,
            LookupTargets.All,
            type => CecilUtilities.SetFieldType(field, type));

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
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseConstantDirective(
        Token directive, Token[] tokens)
    {
        this.FinishCurrentState();

        if (tokens.Length < 5)
        {
            this.OutputError(directive, $"Missing constant variable operand.");
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

        var data = tokens.Skip(4).
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

        var constantTypeNameToken = tokens[2];
        var constantName = tokens[3].Text;

        var fa = scopeDescriptor switch
        {
            ScopeDescriptors.Public => FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly,
            ScopeDescriptors.File => FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly,
            _ => FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly,
        };

        if (this.TryGetField(
            constantName,
            out var f,
            this.fileScopedType,
            LookupTargets.All))
        {
            this.DelayLookingUpType(
                constantTypeNameToken,
                constantTypeNameToken,
                LookupTargets.All,
                type =>
                {
                    if (type.FullName != f.FieldType.FullName)
                    {
                        this.OutputError(
                            tokens[1],
                            $"Mismatched previous constant field type: {constantName}");
                    }
                });
            
            var previousField = f.Resolve();
            if (previousField.Attributes != fa)
            {
                this.OutputError(
                    tokens[1],
                    $"Mismatched previous constant field attribute: {constantName}");
            }

            if (data != null)
            {
                if (!previousField.InitialValue.SequenceEqual(data))
                {
                    this.OutputError(
                        tokens[3],
                        $"Mismatched previous constant field declaration: {constantName}");
                }
            }
            return;
        }
        
        var field = new FieldDefinition(
            constantName,
            fa,
            this.CreateDummyType());

        field.InitialValue = data;

        this.DelayLookingUpType(
            constantTypeNameToken,
            constantTypeNameToken,
            LookupTargets.All,
            type =>
            {
                var fieldType = type;
                if (this.TryGetType("System.Runtime.CompilerServices.IsConst", out var isConstModifierType))
                {
                    fieldType = type.MakeOptionalModifierType(isConstModifierType);
                }
                else
                {
                    this.OutputWarning(
                        constantTypeNameToken,
                        $"IsConst was not found, so not applied. Because maybe did not reference core library.");
                }

                CecilUtilities.SetFieldType(field, fieldType);
            });

        switch (scopeDescriptor)
        {
            case ScopeDescriptors.Public:
            case ScopeDescriptors.Internal:
                this.cabiRDataType.Fields.Add(field);
                break;
            default:
                this.fileScopedType.Fields.Add(field);
                break;
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

        var localTypeNameToken = tokens[1];

        var variable = new VariableDefinition(
            this.CreateDummyType());

        this.DelayLookingUpType(
            localTypeNameToken,
            localTypeNameToken,
            LookupTargets.All,
            type =>
            {
                if (type.IsByReference)
                {
                    var resolved = type.Resolve();
                    var elementType = resolved.GetElementType();
                    if (elementType.IsValueType)
                    {
                        variable.VariableType = type.MakePinnedType();
                        return;
                    }
                }
                variable.VariableType = type;
            });

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
            _ => TypeAttributes.NestedPublic | TypeAttributes.Sealed,
        };
        var valueFieldAttributes =
            FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName;

        var underlyingTypeNameToken = tokens[2];
        var underlyingTypeName = underlyingTypeNameToken.Text;

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

        if (this.TryGetType(
            enumerationTypeName,
            out var etref,
            scopeDescriptor switch
            {
                ScopeDescriptors.File => LookupTargets.File,
                _ => LookupTargets.Assembly,
            }))
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
            _ => TypeAttributes.NestedPublic | TypeAttributes.Sealed,
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

        if (this.TryGetType(
            structureTypeName,
            out var stref,
            scopeDescriptor switch
            {
                ScopeDescriptors.File => LookupTargets.File,
                _ => LookupTargets.Assembly,
            }))
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

            if (st.Fields.Count >= 1)
            {
                this.checkingMemberIndex = 0;
            }

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
                var fullPath = Path.GetFullPath(tokens[2].Text);
                var file = new FileDescriptor(
                    Path.GetDirectoryName(fullPath),
                    Path.GetFileName(fullPath),
                    language,
                    true);
                this.files[tokens[1].Text] = file;
            }
        }
        else
        {
            this.OutputError(
                tokens[3], $"Invalid language operand: {tokens[3].Text}");
        }
    }


    /////////////////////////////////////////////////////////////////////

    private void ParseHiddenDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length > 1)
        {
            this.OutputError(
                tokens[1],
                $"Too many operands.");
            return;
        }

        if (this.produceDebuggingInformation)
        {
            this.currentFile = this.currentCilFile;
            this.queuedLocation = null;
            this.lastLocation = null;
            this.isProducedOriginalSourceCodeLocation = false;
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
            this.currentFile = file;
            var location = new Location(
                file, vs[0], vs[1], vs[2], vs[3]);
            this.queuedLocation = location;
            this.lastLocation = null;
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
            // Constant directive:
            case "constant":
                this.ParseConstantDirective(directive, tokens);
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
            // Hidden directive:
            case "hidden":
                this.ParseHiddenDirective(directive, tokens);
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
