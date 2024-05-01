/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Parsing;
using chibicc.toolchain.Tokenizing;
using chibild.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace chibild.Generating;

partial class TypeGenerator
{
    public static readonly TypeNode[] FixedLengthArrayTypeConstructRequirementTypes = new[]
    {
        TypeParser.UnsafeParse<TypeNode>(
            Token.Identity("System.ValueType")),
        TypeParser.UnsafeParse<TypeNode>(
            Token.Identity("System.Reflection.DefaultMemberAttribute")),
        TypeParser.UnsafeParse<TypeNode>(
            Token.Identity("System.IndexOutOfRangeException")),
    };

    private static int GetTypeSize(TypeReference type) => type.FullName switch
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

    private static bool TryCreateFixedLengthArrayType(
        ModuleDefinition targetModule,
        FixedLengthArrayTypeNode type,
        bool isPublic,
        IReadOnlyDictionary<string, TypeReference> requiredTypes,
        out TypeDefinition atr,
        Action<Token, string> outputError)
    {
        var elementType = requiredTypes[type.ElementType.CilTypeName];
        var length = type.Length;

        var vttr = requiredTypes["System.ValueType"];

        var fullName = type.CilTypeName;
        string ns;
        string name;
        if (fullName.LastIndexOf('.') is { } ni && ni >= 0)
        {
            ns = fullName.Substring(0, ni);
            name = fullName.Substring(ns.Length + 1);
        }
        else
        {
            ns = elementType.Namespace;
            name = fullName;
        }

        var valueArrayType = new TypeDefinition(
            ns,
            name,
            isPublic ?
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout :
                TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            vttr);
        
        // Flex array type has no size, so `ClassSize` is also set to 0.
        // However, CLR does not handle structures of size 0 and always assumes 1 byte.
        // If this type is calculated as `sizeof FooBarArray` it will return 1, which is not as intended.
        // But since Flex array itself is an array whose size cannot be calculated,
        // this problem is ignored.
        if (length < 0)    // Flex array (length == -1)
        {
            valueArrayType.ClassSize = 0;
            valueArrayType.PackingSize = 8;
        }

        ///////////////////////////////

        for (var index = 0; index < length; index++)
        {
            var itemField = new FieldDefinition(
                $"item{index}",
                FieldAttributes.Private,
                elementType);
            valueArrayType.Fields.Add(itemField);

            CecilUtilities.SetFieldType(itemField, elementType);
        }

        ///////////////////////////////

        var dmatr = requiredTypes["System.Reflection.DefaultMemberAttribute"];
        
        if (dmatr.Resolve().
            Methods.
            FirstOrDefault(m =>
                m is { IsPublic: true, IsStatic: false, IsConstructor: true, } &&
                m.Parameters.Count == 1 &&
                m.Parameters[0].ParameterType.FullName == "System.String") is
            not MethodReference dmacr)
        {
            atr = null!;
            outputError(
                type.Token,
                $"Could not find a constructor: System.Reflection.DefaultMemberAttribute..ctor");
            return false;
        }

        dmacr = CecilUtilities.SafeImport(targetModule, dmacr);

        var defaultMemberCustomAttribute = new CustomAttribute(dmacr);
        defaultMemberCustomAttribute.ConstructorArguments.Add(new(
            targetModule.TypeSystem.String,
            "Item"));

        valueArrayType.CustomAttributes.Add(defaultMemberCustomAttribute);

        ///////////////////////////////

        if (length >= 0)
        {
            var getLengthMethod = new MethodDefinition(
                "get_Length",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                targetModule.TypeSystem.Int32);
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
                targetModule.TypeSystem.Int32);
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
            targetModule.TypeSystem.Int32));
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

        var iooretr = requiredTypes["System.IndexOutOfRangeException"];

        if (iooretr.Resolve().
            Methods.
            FirstOrDefault(m =>
                m is { IsPublic: true, IsStatic: false, IsConstructor: true, } &&
                m.Parameters.Count == 0) is
            not MethodReference ioorecr)
        {
            atr = null!;
            outputError(
                type.Token,
                $"Could not find a constructor: System.IndexOutOfRangeException..ctor");
            return false;
        }

        ioorecr = CecilUtilities.SafeImport(targetModule, ioorecr);

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
                Instruction.Create(OpCodes.Newobj, ioorecr));
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

        var setItemMethod = new MethodDefinition(
            "set_Item",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            targetModule.TypeSystem.Void);
        setItemMethod.HasThis = true;
        setItemMethod.Parameters.Add(new(
            "index",
            ParameterAttributes.None,
            targetModule.TypeSystem.Int32));
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
                Instruction.Create(OpCodes.Newobj, ioorecr));
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

        // TODO: implemente `IList<T>`, `IReadOnlyList<T>`
        
        setItemInstructions.Add(
            Instruction.Create(OpCodes.Ret));

        atr = valueArrayType;
        return true;
    }

    public static bool IsInteriorTypesPublic(
        TypeNode type,
        IReadOnlyDictionary<string, TypeReference> requiredTypes)
    {
        static bool InnerFilter(
            TypeNode type,
            IReadOnlyDictionary<string, TypeReference> requiredTypes)
        {
            if (requiredTypes.TryGetValue(type.CilTypeName, out var tr) &&
                tr.Resolve() is { } td1)
            {
                return td1.IsPublic || td1.IsNestedPublic;
            }
            
            switch (type)
            {
                // .NET array type.
                case ArrayTypeNode(var elementType, _):
                    return InnerFilter(elementType, requiredTypes);

                // Fixed length array type.
                case FixedLengthArrayTypeNode(var elementType, _, _):
                    return InnerFilter(elementType, requiredTypes);

                // Reference/Pointer type.
                case DerivedTypeNode(_, var elementType, _):
                    return InnerFilter(elementType, requiredTypes);
                
                // Function signature.
                case FunctionSignatureNode(var returnType, var parameters, _, _):
                    var isPublic = InnerFilter(returnType, requiredTypes);
                    foreach (var parameter in parameters)
                    {
                        isPublic &= InnerFilter(parameter.ParameterType, requiredTypes);
                    }
                    return isPublic;

                default:
                    throw new InvalidOperationException();
            }
        }

        return InnerFilter(type, requiredTypes);
    }

    public static bool TryGetFixedLengthArrayType(
        ModuleDefinition targetModule,
        FixedLengthArrayTypeNode type,
        IReadOnlyDictionary<string, TypeReference> requiredTypes,
        out TypeReference flatr,
        Action<Token, string> outputError)
    {
        var isPublic = IsInteriorTypesPublic(type, requiredTypes);
        
        if (targetModule.GetType(type.CilTypeName) is not { } flatd)
        {
            if (!TryCreateFixedLengthArrayType(
                targetModule,
                type,
                isPublic,
                requiredTypes,
                out flatd,
                outputError))
            {
                flatr = null!;
                return false;
            }
            targetModule.Types.Add(flatd);
        }
        
        flatr = flatd;
        return true;
    }
}
