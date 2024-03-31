/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibild.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Globalization;
using System.Linq;

namespace chibild.Parsing;

partial class Parser
{
    private static int GetTypeSize(TypeReference type) =>
        type.FullName switch
        {
            "System.Boolean" => 1,
            "System.Byte" => 1,
            "System.SByte" => 1,
            "System.Int16" => 2,
            "System.UInt16" => 2,
            "System.Char" => 2,
            "System.Int32" => 4,
            "System.UInt32" => 4,
            "System.Int64" => 8,
            "System.UInt64" => 8,
            "System.Single" => 4,
            "System.Double" => 8,
            _ => -1,
        };
    private static FunctionSignatureNode defaultMemberCtorSignature =
        TypeParser.UnsafeParse<FunctionSignatureNode>("void(string)");

    private TypeDefinition CreateValueArrayType(
        string valueArrayTypeNamespace,
        string valueArrayTypeName,
        int length,
        TypeReference elementType,
        bool isReadOnly)
    {
        var et = elementType.Resolve();
        var scopeDescriptor = et.IsPublic ?
            ScopeDescriptors.Public : et.IsNestedPublic ?
            ScopeDescriptors.File :
            ScopeDescriptors.Internal;

        var valueArrayType = new TypeDefinition(
            valueArrayTypeNamespace,
            valueArrayTypeName,
            scopeDescriptor switch
            {
                ScopeDescriptors.Public => TypeAttributes.Public,
                ScopeDescriptors.Internal => TypeAttributes.NotPublic,
                _ => TypeAttributes.NestedPublic,
            } | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            this.systemValueTypeType.Value);

        if (length < 0)
        {
            valueArrayType.ClassSize = 0;
            valueArrayType.PackingSize = 8;
        }

        switch (scopeDescriptor)
        {
            case ScopeDescriptors.Public:
            case ScopeDescriptors.Internal:
                this.module.Types.Add(valueArrayType);
                break;
            case ScopeDescriptors.File:
                et.DeclaringType.NestedTypes.Add(valueArrayType);
                break;
        }

        ///////////////////////////////

        for (var index = 0; index < length; index++)
        {
            var itemField = new FieldDefinition(
                $"item{index}", FieldAttributes.Private, elementType);
            valueArrayType.Fields.Add(itemField);

            CecilUtilities.SetFieldType(itemField, elementType);
        }

        ///////////////////////////////

        if (this.TryGetMethod(
            "System.Reflection.DefaultMemberAttribute..ctor",
            defaultMemberCtorSignature,
            out var defaultMemberAttributeConstructor))
        {
            var defaultMemberCustomAttribute = new CustomAttribute(
                defaultMemberAttributeConstructor);
            defaultMemberCustomAttribute.ConstructorArguments.Add(
                new(this.UnsafeGetType("System.String"), "Item"));

            valueArrayType.CustomAttributes.Add(defaultMemberCustomAttribute);
        }

        ///////////////////////////////

        if (length >= 0)
        {
            var getLengthMethod = new MethodDefinition(
                "get_Length",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                this.UnsafeGetType("System.Int32"));
            getLengthMethod.HasThis = true;
            valueArrayType.Methods.Add(getLengthMethod);

            getLengthMethod.Body.Instructions.Add(
                Instruction.Create(OpCodes.Ldc_I4, length));
            getLengthMethod.Body.Instructions.Add(
                Instruction.Create(OpCodes.Ret));

            ///////////////////////////////

            var lengthProperty = new PropertyDefinition(
                "Length",
                PropertyAttributes.None,
                this.UnsafeGetType("System.Int32"));
            valueArrayType.Properties.Add(lengthProperty);

            lengthProperty.GetMethod = getLengthMethod;
        }

        ///////////////////////////////

        var indexerProperty = new PropertyDefinition(
            "Item",
            PropertyAttributes.None,
            elementType);
        valueArrayType.Properties.Add(indexerProperty);

        ///////////////////////////////

        var getItemMethod = new MethodDefinition(
            "get_Item",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            elementType);
        getItemMethod.HasThis = true;
        getItemMethod.Parameters.Add(new(
            "index",
            ParameterAttributes.None,
            this.UnsafeGetType("System.Int32")));
        valueArrayType.Methods.Add(getItemMethod);

        // Special case: Force 1 byte footprint on boolean type.
        if (elementType.FullName == "System.Boolean")
        {
            getItemMethod.MethodReturnType.MarshalInfo = new(NativeType.U1);
        }
        else if (elementType.FullName == "System.Char")
        {
            getItemMethod.MethodReturnType.MarshalInfo = new(NativeType.U2);
        }

        indexerProperty.GetMethod = getItemMethod;

        ///////////////////////////////

        var getItemInstructions = getItemMethod.Body.Instructions;

        var elementTypeSize = GetTypeSize(elementType);

        if (length >= 0)
        {
            // Guard
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Ldarg_1));
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Ldc_I4, length));
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Clt));
            var getItemNext = Instruction.Create(OpCodes.Ldarg_1);
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Brtrue_S, getItemNext));

            getItemInstructions.Add(
                Instruction.Create(OpCodes.Newobj, this.indexOutOfRangeCtor.Value));
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Throw));
            getItemInstructions.Add(getItemNext);
        }
        else
        {
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Ldarg_1));
        }

        // Body
        if (elementTypeSize >= 2)
        {
            getItemInstructions.Add(
                Instruction.Create(
                    OpCodes.Ldc_I4_S, (sbyte)elementTypeSize));
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Mul));
        }
        else if (elementTypeSize == -1)
        {
            getItemInstructions.Add(
                Instruction.Create(
                    OpCodes.Sizeof, elementType));
            getItemInstructions.Add(
                Instruction.Create(OpCodes.Mul));
        }
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Ldarg_0));
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Conv_U));
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Add));
        switch (elementType.FullName)
        {
            case "System.Boolean":
            case "System.Byte":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_U1));
                break;
            case "System.SByte":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I1));
                break;
            case "System.Int16":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I2));
                break;
            case "System.Char":
            case "System.UInt16":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_U2));
                break;
            case "System.Int32":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I4));
                break;
            case "System.UInt32":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_U4));
                break;
            case "System.Int64":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I8));
                break;
            case "System.UInt64":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I8));
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Conv_U8));
                break;
            case "System.Single":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_R4));
                break;
            case "System.Double":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_R8));
                break;
            case "System.IntPtr":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I));
                break;
            case "System.UIntPtr":
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldind_I));
                getItemInstructions.Add(
                    Instruction.Create(OpCodes.Conv_U));
                break;
            default:
                if (elementType.IsValueType)
                {
                    getItemInstructions.Add(
                        Instruction.Create(OpCodes.Ldobj, elementType));
                }
                else
                {
                    getItemInstructions.Add(
                        Instruction.Create(OpCodes.Ldind_Ref));
                }
                break;
        }
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Ret));

        ///////////////////////////////

        if (!isReadOnly)
        {
            var setItemMethod = new MethodDefinition(
                "set_Item",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                this.UnsafeGetType("System.Void"));
            setItemMethod.HasThis = true;
            setItemMethod.Parameters.Add(new(
                "index",
                ParameterAttributes.None,
                this.UnsafeGetType("System.Int32")));
            var valueParameter = new ParameterDefinition(
                "value",
                ParameterAttributes.None,
                elementType);
            setItemMethod.Parameters.Add(valueParameter);

            // Special case: Force 1 byte footprint on boolean type.
            if (elementType.FullName == "System.Boolean")
            {
                valueParameter.MarshalInfo = new(NativeType.U1);
            }
            else if (elementType.FullName == "System.Char")
            {
                valueParameter.MarshalInfo = new(NativeType.U2);
            }

            valueArrayType.Methods.Add(setItemMethod);

            indexerProperty.SetMethod = setItemMethod;

            ///////////////////////////////

            var setItemInstructions = setItemMethod.Body.Instructions;

            if (length >= 0)
            {
                // Guard
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldarg_1));
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldc_I4, length));
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Clt));
                var setItemNext = Instruction.Create(OpCodes.Ldarg_1);
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Brtrue_S, setItemNext));

                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Newobj, this.indexOutOfRangeCtor.Value));
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Throw));
                setItemInstructions.Add(setItemNext);
            }
            else
            {
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Ldarg_1));

            }

            // Body
            if (elementTypeSize >= 2)
            {
                setItemInstructions.Add(
                    Instruction.Create(
                        OpCodes.Ldc_I4_S, (sbyte)elementTypeSize));
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Mul));
            }
            else if (elementTypeSize == -1)
            {
                setItemInstructions.Add(
                    Instruction.Create(
                        OpCodes.Sizeof, elementType));
                setItemInstructions.Add(
                    Instruction.Create(OpCodes.Mul));
            }
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Ldarg_0));
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Conv_U));
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Add));
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Ldarg_2));
            switch (elementType.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_I1));
                    break;
                case "System.Int16":
                case "System.UInt16":
                case "System.Char":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_I2));
                    break;
                case "System.Int32":
                case "System.UInt32":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_I4));
                    break;
                case "System.Int64":
                case "System.UInt64":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_I8));
                    break;
                case "System.Single":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_R4));
                    break;
                case "System.Double":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_R8));
                    break;
                case "System.IntPtr":
                case "System.UIntPtr":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_I));
                    break;
                default:
                    if (elementType.IsValueType)
                    {
                        setItemInstructions.Add(
                            Instruction.Create(OpCodes.Stobj, elementType));
                    }
                    else
                    {
                        setItemInstructions.Add(
                            Instruction.Create(OpCodes.Stind_Ref));
                    }
                    break;
            }
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Ret));
        }

        return valueArrayType;
    }

    private string GetValueArrayTypeName(string name)
    {
        switch (name[name.Length - 1])
        {
            case '*':
                return $"{this.GetValueArrayTypeName(name.Substring(0, name.Length - 1))}_ptr";
            case '&':
                return $"{this.GetValueArrayTypeName(name.Substring(0, name.Length - 1))}_ref";
            case ']' when name.Length >= 4:
                var startBracketIndex = name.LastIndexOf('[', name.Length - 2);
                // "aaa"
                if (startBracketIndex >= 1)
                {
                    var elementTypeName = name.Substring(0, startBracketIndex);
                    // "aaa[]"
                    if ((name.Length - startBracketIndex - 2) == 0)
                    {
                        return $"{this.GetValueArrayTypeName(elementTypeName)}_arr";
                    }
                    // "aaa[10]", "aaa[*]"
                    else
                    {
                        var lengthString =
                            name.Substring(startBracketIndex + 1, name.Length - startBracketIndex - 2);
                        if (lengthString == "*")
                        {
                            // "aaa_flex"
                            return $"{this.GetValueArrayTypeName(elementTypeName)}_flex";
                        }
                        else
                        {
                            var length = int.Parse(
                                lengthString,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture);

                            // "aaa_len10"
                            return $"{this.GetValueArrayTypeName(elementTypeName)}_len{length}";
                        }
                    }
                }
                break;
        }

        return name;
    }

    private TypeReference GetValueArrayType(
        TypeReference elementType,
        int length,
        TypeDefinition? fileScopedType,
        LookupTargets lookupTargets)
    {
        var valueArrayTypeName = length >= 0 ?
            $"{this.GetValueArrayTypeName(elementType.Name)}_len{length}" :
            $"{this.GetValueArrayTypeName(elementType.Name)}_flex";
        var valueArrayTypeFullName = elementType.Namespace switch
        {
            "C.type" => valueArrayTypeName,
            "" => valueArrayTypeName,
            _ => $"{elementType.Namespace}.{valueArrayTypeName}",
        };

        if (!this.TryLookupTypeByTypeName(
            valueArrayTypeFullName,
            out var valueArrayType,
            fileScopedType,
            lookupTargets))
        {
            valueArrayType = this.CreateValueArrayType(
                elementType.Namespace,
                valueArrayTypeName,
                length,
                elementType,
                false);
        }

        return valueArrayType;
    }
}
