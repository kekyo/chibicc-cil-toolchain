/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System;
using System.Linq;

namespace chibias.Internal;

partial class Parser
{
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

    private bool TryConstructTypeFromNode(
        TypeNode typeNode,
        out TypeReference type,
        TypeDefinition? fileScopedType)
    {
        switch (typeNode)
        {
            // Derived types (pointer and reference)
            case DerivedTypeNode dtn:
                switch (dtn.Type)
                {
                    // "System.Int32*"
                    case DerivedTypes.Pointer:
                        // Special case: Function pointer "System.String(System.Int32&,System.Int8)*"
                        if (dtn.ElementType is FunctionSignatureNode fsn)
                        {
                            if (this.TryConstructTypeFromNode(
                                fsn.ReturnType, out var returnType, fileScopedType))
                            {
                                var fpt = new FunctionPointerType()
                                {
                                    ReturnType = returnType,
                                    CallingConvention = fsn.CallingConvention,
                                    HasThis = false,
                                    ExplicitThis = false,
                                };

                                foreach (var parameterNode in fsn.ParameterTypes)
                                {
                                    if (this.TryConstructTypeFromNode(
                                        parameterNode, out var parameterType, fileScopedType))
                                    {
                                        fpt.Parameters.Add(new(parameterType));
                                    }
                                    else
                                    {
                                        type = null!;
                                        return false;
                                    }
                                }

                                type = fpt;
                                return true;
                            }
                            else
                            {
                                type = null!;
                                return false;
                            }
                        }

                        if (this.TryConstructTypeFromNode(
                            dtn.ElementType, out var elementType, fileScopedType))
                        {
                            type = new PointerType(elementType);
                            return true;
                        }

                        type = null!;
                        return false;

                    // "System.Int32&"
                    case DerivedTypes.Reference:
                        if (this.TryConstructTypeFromNode(
                            dtn.ElementType, out var elementType2, fileScopedType))
                        {
                            type = new ByReferenceType(elementType2);
                            return true;
                        }

                        type = null!;
                        return false;

                    default:
                        throw new InvalidOperationException();
                }

            // Array types
            case ArrayTypeNode atn:
                if (this.TryConstructTypeFromNode(
                    atn.ElementType, out var elementType3, fileScopedType))
                {
                    // "System.Int32[13]"
                    if (atn.Length is { } length)
                    {
                        // "System.Int32_len13"
                        type = this.GetValueArrayType(elementType3, length);
                        return true;
                    }

                    // "System.Int32[]"
                    type = new ArrayType(elementType3);
                    return true;
                }

                type = null!;
                return false;

            // Other types
            case TypeIdentityNode tin:
                // IMPORTANT ORDER:
                //   Will lookup before this module, because the types redefinition by C headers
                //   each assembly (by generating chibias).
                //   Always we use first finding type, silently ignored when multiple declarations.
                if (fileScopedType?.NestedTypes.FirstOrDefault(type =>
                    type.Name == tin.Identity) is { } td2)
                {
                    type = td2;
                    return true;
                }

                if (this.module.Types.FirstOrDefault(type =>
                    (type.Namespace == "C.type" && type.Name == tin.Identity) ||
                    // FullName is needed because the value array type name is not CABI.
                    (type.FullName == tin.Identity)) is { } td3)
                {
                    type = td3;
                    return true;
                }

                if (this.cabiSpecificSymbols.TryGetMember<TypeReference>(tin.Identity, out var tr1))
                {
                    type = this.Import(tr1);
                    return true;
                }

                if (this.importantTypes.TryGetValue(tin.Identity, out type!))
                {
                    return true;
                }

                if (CecilUtilities.TryLookupOriginTypeName(tin.Identity, out var originTypeName))
                {
                    return this.TryGetType(
                        originTypeName,
                        out type,
                        fileScopedType);
                }

                if (this.referenceTypes.TryGetMember(tin.Identity, out var td4))
                {
                    type = this.Import(td4);
                    return true;
                }

                type = null!;
                return false;

            // Function signature
            case FunctionSignatureNode _:
                // Invalid format:
                //   Function signature (NOT function pointer type) cannot be attributed to .NET type.
                type = null!;
                return false;

            default:
                type = null!;
                return false;
        }
    }

    private bool TryGetType(
        string typeName,
        out TypeReference type) =>
        this.TryGetType(typeName, out type, this.fileScopedType);

    private bool TryGetType(
        string typeName,
        out TypeReference type,
        TypeDefinition? fileScopedType)
    {
        if (!TypeParser.TryParse(typeName, out var rootTypeNode))
        {
            type = null!;
            return false;
        }

        return this.TryConstructTypeFromNode(rootTypeNode, out type, fileScopedType);
    }

    /////////////////////////////////////////////////////////////////////

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

            if (this.cabiTextType.Methods.
                FirstOrDefault(method => method.Name == methodName) is { } m3)
            {
                method = m3;
                return true;
            }

            if (this.cabiSpecificSymbols.TryGetMember<MethodReference>(methodName, out var m))
            {
                method = this.Import(m);
                return true;
            }

            method = null!;
            return false;
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

        method = null!;
        return false;
    }

    /////////////////////////////////////////////////////////////////////

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
            
            if (this.cabiDataType.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f3)
            {
                field = f3;
                return true;
            }

            if (this.cabiSpecificSymbols.TryGetMember<FieldReference>(name, out var f))
            {
                field = this.Import(f);
                return true;
            }

            field = null!;
            return false;
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

        field = null!;
        return false;
    }

    /////////////////////////////////////////////////////////////////////

    private bool TryGetMember(
        string memberName,
        string[] functionParameterTypeNames,
        out MemberReference member,
        TypeDefinition fileScopedType)
    {
        if (this.TryGetMethod(
            memberName, functionParameterTypeNames, out var method, fileScopedType))
        {
            member = method;
            return true;
        }
        
        if (this.TryGetField(
            memberName, out var field, fileScopedType))
        {
            member = field;
            return true;
        }
        
        if (this.TryGetType(
            memberName, out var type, fileScopedType))
        {
            member = type;
            return true;
        }

        member = null!;
        return false;
    }

    private bool TryGetMember(
        string memberName,
        string[] functionParameterTypeNames,
        out MemberReference member) =>
        this.TryGetMember(
            memberName, functionParameterTypeNames, out member, this.fileScopedType);

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

    /////////////////////////////////////////////////////////////////////

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

    /////////////////////////////////////////////////////////////////////

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

    private void DelayLookingUpMember(
        string memberName,
        Token memberNameToken,
        string[] parameterTypeNames,
        Action<MemberReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMember(
                memberName,
                parameterTypeNames,
                out var member,
                capturedFileScopedType))
            {
                action(member);
            }
            else
            {
                this.OutputError(
                    memberNameToken,
                    $"Could not find member: {memberName}");
            }
        });
    }

    private void DelayLookingUpMember(
        Token memberNameToken,
        string[] parameterTypeNames,
        Action<MemberReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMember(
                memberNameToken.Text,
                parameterTypeNames,
                out var member,
                capturedFileScopedType))
            {
                action(member);
            }
            else
            {
                this.OutputError(
                    memberNameToken,
                    $"Could not find member: {memberNameToken.Text}");
            }
        });
    }
}
