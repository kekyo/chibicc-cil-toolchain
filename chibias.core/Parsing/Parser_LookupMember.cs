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
using Mono.Cecil.Rocks;
using System;
using System.Diagnostics;
using System.Linq;

namespace chibias.Parsing;

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
        if (!this.TryGetType(
            typeName,
            out var type,
            null,
            LookupTargets.All))
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
        if (!this.TryGetMethod(
            methodName,
            parameterTypeNames,
            out var method,
            null,
            LookupTargets.All))
        {
            this.caughtError = true;
            this.logger.Error($"Could not find for important method: {methodName}");

            method = this.CreateDummyMethod();
        }
        return method;
    }

    /////////////////////////////////////////////////////////////////////

    [Flags]
    private enum LookupTargets
    {
        Assembly = 0x01,
        File = 0x02,
        CAbiSpecific = 0x04,
        Important = 0x08,
        References = 0x10,
        All = 0x1f,
    }

    private bool TryLookupTypeByTypeName(
        string typeName,
        out TypeReference type,
        TypeDefinition? fileScopedType,
        LookupTargets lookupTargets)
    {
        // IMPORTANT ORDER:
        //   Will lookup before this module, because the types redefinition by C headers
        //   each assembly (by generating chibias).
        //   Always we use first finding type, silently ignored when multiple declarations.
        if (lookupTargets.HasFlag(LookupTargets.File) &&
            fileScopedType?.NestedTypes.FirstOrDefault(type =>
                type.Name == typeName) is { } td2)
        {
            type = td2;
            return true;
        }

        if (lookupTargets.HasFlag(LookupTargets.Assembly) &&
            this.module.Types.FirstOrDefault(type =>
            (type.Namespace == "C.type" && type.Name == typeName) ||
            // FullName is needed because the value array type name is not CABI.
            (type.FullName == typeName)) is { } td3)
        {
            type = td3;
            return true;
        }

        if (lookupTargets.HasFlag(LookupTargets.CAbiSpecific) &&
            this.cabiSpecificSymbols.TryGetMember<TypeReference>(typeName, out var tr1))
        {
            type = this.Import(tr1);
            return true;
        }

        if (lookupTargets.HasFlag(LookupTargets.Important) &&
            this.importantTypes.TryGetValue(typeName, out type!))
        {
            return true;
        }

        if (CecilUtilities.TryLookupOriginTypeName(typeName, out var originTypeName))
        {
            return this.TryGetType(
                originTypeName,
                out type,
                fileScopedType,
                lookupTargets);
        }

        if (lookupTargets.HasFlag(LookupTargets.References) &&
            this.referenceTypes.TryGetMember(typeName, out var td4))
        {
            type = this.Import(td4);
            return true;
        }

        type = null!;
        return false;
    }

    private bool TryConstructTypeFromNode(
        TypeNode typeNode,
        out TypeReference type,
        TypeDefinition? fileScopedType,
        LookupTargets lookupTargets)
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
                                fsn.ReturnType, out var returnType, fileScopedType, lookupTargets))
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
                                        parameterNode, out var parameterType, fileScopedType, lookupTargets))
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
                            dtn.ElementType, out var elementType, fileScopedType, lookupTargets))
                        {
                            type = new PointerType(elementType);
                            return true;
                        }

                        type = null!;
                        return false;

                    // "System.Int32&"
                    case DerivedTypes.Reference:
                        if (this.TryConstructTypeFromNode(
                            dtn.ElementType, out var elementType2, fileScopedType, lookupTargets))
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
                    atn.ElementType, out var elementType3, fileScopedType, lookupTargets))
                {
                    // "System.Int32[13]"
                    if (atn.Length is { } length)
                    {
                        // "System.Int32_len13"
                        type = this.GetValueArrayType(
                            elementType3, length, fileScopedType, lookupTargets);
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
                return this.TryLookupTypeByTypeName(
                    tin.Identity,
                    out type,
                    fileScopedType,
                    lookupTargets);

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
        out TypeReference type,
        LookupTargets lookupTargets = LookupTargets.All) =>
        this.TryGetType(typeName, out type, this.fileScopedType, lookupTargets);

    private bool TryGetType(
        string typeName,
        out TypeReference type,
        TypeDefinition? fileScopedType,
        LookupTargets lookupTargets)
    {
        if (!TypeParser.TryParse(typeName, out var rootTypeNode))
        {
            type = null!;
            return false;
        }

        return this.TryConstructTypeFromNode(rootTypeNode, out type, fileScopedType, lookupTargets);
    }

    /////////////////////////////////////////////////////////////////////

    private bool TryGetMethod(
        string name,
        string[] parameterTypeNames,
        out MethodReference method,
        LookupTargets lookupTargets = LookupTargets.All) =>
        this.TryGetMethod(
            name, parameterTypeNames, out method, this.fileScopedType, lookupTargets);

    private bool TryGetMethod(
        string name,
        string[] parameterTypeNames,
        out MethodReference method,
        TypeDefinition? fileScopedType,
        LookupTargets lookupTargets)
    {
        var methodNameIndex = name.LastIndexOf('.');
        var methodName = name.Substring(methodNameIndex + 1);

        if (methodName == "ctor" || methodName == "cctor")
        {
            methodName = "." + methodName;
            methodNameIndex--;
        }

        var strictParameterTypeNames = parameterTypeNames.
            Select(parameterTypeName => this.TryGetType(
                parameterTypeName,
                out var type,
                fileScopedType,
                LookupTargets.All) ?
                    type.FullName : string.Empty).
            ToArray();

        if (strictParameterTypeNames.Contains(string.Empty))
        {
            method = null!;
            return false;
        }

        // CABI specific case: No need to check any parameters except variadics.
        if (methodNameIndex <= 0)
        {
            MethodReference? foundMethod = null;
            if (lookupTargets.HasFlag(LookupTargets.File) &&
                fileScopedType?.Methods.
                FirstOrDefault(method => method.Name == methodName) is { } m2 &&
                CecilUtilities.IsValidCAbiParameter(m2, strictParameterTypeNames))
            {
                foundMethod = m2;
            }
            else if (lookupTargets.HasFlag(LookupTargets.Assembly) && 
                this.cabiTextType.Methods.
                FirstOrDefault(method => method.Name == methodName) is { } m3 &&
                CecilUtilities.IsValidCAbiParameter(m3, strictParameterTypeNames))
            {
                foundMethod = m3;
            }
            else if (lookupTargets.HasFlag(LookupTargets.CAbiSpecific) && 
                this.cabiSpecificSymbols.TryGetMember<MethodReference>(methodName, out var m) &&
                CecilUtilities.IsValidCAbiParameter(m, strictParameterTypeNames))
            {
                foundMethod = this.Import(m);
            }

            if (foundMethod == null)
            {
                method = null!;
                return false;
            }

            if (foundMethod.CallingConvention != MethodCallingConvention.VarArg)
            {
                method = foundMethod;
                return true;
            }

            // Will make specialized method reference with variadic parameters.
            var m5 = new MethodReference(
                foundMethod.Name, foundMethod.ReturnType, foundMethod.DeclaringType);
            m5.CallingConvention = MethodCallingConvention.VarArg;
            foreach (var parameter in foundMethod.Parameters)
            {
                m5.Parameters.Add(new(
                    parameter.Name, parameter.Attributes, parameter.ParameterType));
            }

            // Append sentinel parameters each parameter types.
            var first = true;
            foreach (var parameterTypeName in
                strictParameterTypeNames.Skip(foundMethod.Parameters.Count))
            {
                if (!this.TryGetType(
                    parameterTypeName,
                    out var parameterType,
                    fileScopedType,
                    LookupTargets.All))
                {
                    method = null!;
                    return false;
                }

                if (first)
                {
                    m5.Parameters.Add(new(parameterType.MakeSentinelType()));
                    first = false;
                }
                else
                {
                    m5.Parameters.Add(new(parameterType));
                }
            }

            method = m5;
            return true;
        }

        var typeName = name.Substring(0, methodNameIndex);

        if (!this.referenceTypes.TryGetMember(typeName, out var type))
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
        out FieldReference field,
        LookupTargets lookupTargets = LookupTargets.All) =>
        this.TryGetField(name, out field, this.fileScopedType, lookupTargets);

    private bool TryGetField(
        string name,
        out FieldReference field,
        TypeDefinition? fileScopedType,
        LookupTargets lookupTargets)
    {
        var fieldNameIndex = name.LastIndexOf('.');
        var fieldName = name.Substring(fieldNameIndex + 1);
        if (fieldNameIndex <= 0)
        {
            if (lookupTargets.HasFlag(LookupTargets.File) &&
                fileScopedType?.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f2)
            {
                field = f2;
                return true;
            }
            
            if (lookupTargets.HasFlag(LookupTargets.Assembly) && 
                this.cabiDataType.Fields.
                FirstOrDefault(field => field.Name == fieldName) is { } f3)
            {
                field = f3;
                return true;
            }

            if (lookupTargets.HasFlag(LookupTargets.CAbiSpecific) &&
                this.cabiSpecificSymbols.TryGetMember<FieldReference>(name, out var f))
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
        TypeDefinition fileScopedType,
        LookupTargets lookupTargets)
    {
        if (this.TryGetMethod(
            memberName,
            functionParameterTypeNames,
            out var method,
            fileScopedType,
            lookupTargets))
        {
            member = method;
            return true;
        }
        
        if (this.TryGetField(
            memberName,
            out var field,
            fileScopedType,
            lookupTargets))
        {
            member = field;
            return true;
        }
        
        if (this.TryGetType(
            memberName,
            out var type,
            fileScopedType,
            lookupTargets))
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
        out MemberReference member,
        LookupTargets lookupTargets) =>
        this.TryGetMember(
            memberName, functionParameterTypeNames, out member, this.fileScopedType, lookupTargets);

    /////////////////////////////////////////////////////////////////////

    private void DelayLookingUpType(
        string typeName,
        Token typeNameToken,
        LookupTargets lookupTargets,
        Action<TypeReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetType(
                typeName,
                out var type,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<TypeReference> action) =>
        this.DelayLookingUpType(
            typeNameToken.Text,
            typeNameToken,
            lookupTargets,
            action);

    private void DelayLookingUpType(
        string typeName,
        Location location,
        LookupTargets lookupTargets,
        Action<TypeReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetType(
                typeName,
                out var type,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<FieldReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(
                fieldName,
                out var field,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<FieldReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(
                fieldNameToken.Text,
                out var field,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<FieldReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetField(
                fieldName,
                out var field,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<MethodReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMethod(
                methodNameToken.Text,
                parameterTypeNames,
                out var method,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<MethodReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMethod(
                methodName,
                parameterTypeNames,
                out var method,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<MemberReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMember(
                memberName,
                parameterTypeNames,
                out var member,
                capturedFileScopedType,
                lookupTargets))
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
        LookupTargets lookupTargets,
        Action<MemberReference> action)
    {
        var capturedFileScopedType = this.fileScopedType;
        this.delayedLookupLocalMemberActions.Add(() =>
        {
            if (this.TryGetMember(
                memberNameToken.Text,
                parameterTypeNames,
                out var member,
                capturedFileScopedType,
                lookupTargets))
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
