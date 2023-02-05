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
using System.Globalization;

namespace chibias.Internal;

partial class Parser
{
    private TypeDefinition CreateValueArrayType(
        string valueArrayTypeNamespace,
        string valueArrayTypeName,
        int length,
        TypeReference elementType)
    {
        var valueArrayType = new TypeDefinition(
            valueArrayTypeNamespace,
            valueArrayTypeName,
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            this.valueType.Value);
        //valueArrayType.PackingSize = 1;
        //valueArrayType.ClassSize = 0;
        this.module.Types.Add(valueArrayType);

        for (var index = 0; index < length; index++)
        {
            var itemField = new FieldDefinition(
                $"item{index}", FieldAttributes.Private, elementType);
            valueArrayType.Fields.Add(itemField);
        }

        ///////////////////////////////

        if (this.TryGetMethod(
            "System.Reflection.DefaultMemberAttribute..ctor",
            new[] { "string" },
            out var defaultMemberAttributeConstructor))
        {
            var defaultMemberCustomAttribute = new CustomAttribute(
                defaultMemberAttributeConstructor);
            defaultMemberCustomAttribute.ConstructorArguments.Add(
                new(this.module.TypeSystem.String, "Item"));

            valueArrayType.CustomAttributes.Add(defaultMemberCustomAttribute);
        }

        ///////////////////////////////

        var getLengthMethod = new MethodDefinition(
            "get_Length",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            this.module.TypeSystem.Int32);
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
            this.module.TypeSystem.Int32);
        valueArrayType.Properties.Add(lengthProperty);

        lengthProperty.GetMethod = getLengthMethod;

        ///////////////////////////////

        var getItemMethod = new MethodDefinition(
            "get_Item",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            elementType);
        getItemMethod.HasThis = true;
        getItemMethod.Parameters.Add(new(
            "index",
            ParameterAttributes.None,
            this.module.TypeSystem.Int32));
        valueArrayType.Methods.Add(getItemMethod);

        ///////////////////////////////

        // TODO: IndexOutOfRangeException

        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ldarg_1));
        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Sizeof, elementType));
        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Mul));
        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ldarg_0));
        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Conv_U));
        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Add));
        switch (elementType.FullName)
        {
            case "System.Byte":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_U1));
                break;
            case "System.SByte":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I1));
                break;
            case "System.Int16":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I2));
                break;
            case "System.UInt16":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_U2));
                break;
            case "System.Int32":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I4));
                break;
            case "System.UInt32":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_U4));
                break;
            case "System.Int64":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I8));
                break;
            case "System.UInt64":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I8));
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Conv_U8));
                break;
            case "System.Single":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_R4));
                break;
            case "System.Double":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_R8));
                break;
            case "System.IntPtr":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I));
                break;
            case "System.UIntPtr":
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Ldind_I));
                getItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Conv_U));
                break;
            default:
                if (elementType.IsValueType)
                {
                    getItemMethod.Body.Instructions.Add(
                        Instruction.Create(OpCodes.Ldobj, elementType));
                }
                else
                {
                    getItemMethod.Body.Instructions.Add(
                        Instruction.Create(OpCodes.Ldind_Ref));
                }
                break;
        }
        getItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ret));

        ///////////////////////////////

        var setItemMethod = new MethodDefinition(
            "set_Item",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            this.module.TypeSystem.Void);
        setItemMethod.HasThis = true;
        setItemMethod.Parameters.Add(new(
            "index",
            ParameterAttributes.None,
            this.module.TypeSystem.Int32));
        setItemMethod.Parameters.Add(new(
            "value",
            ParameterAttributes.None,
            elementType));
        valueArrayType.Methods.Add(setItemMethod);

        ///////////////////////////////

        // TODO: IndexOutOfRangeException

        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ldarg_1));
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Sizeof, elementType));
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Mul));
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ldarg_0));
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Conv_U));
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Add));
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ldarg_2));
        switch (elementType.FullName)
        {
            case "System.Byte":
            case "System.SByte":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_I1));
                break;
            case "System.Int16":
            case "System.UInt16":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_I2));
                break;
            case "System.Int32":
            case "System.UInt32":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_I4));
                break;
            case "System.Int64":
            case "System.UInt64":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_I8));
                break;
            case "System.Single":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_R4));
                break;
            case "System.Double":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_R8));
                break;
            case "System.IntPtr":
            case "System.UIntPtr":
                setItemMethod.Body.Instructions.Add(
                    Instruction.Create(OpCodes.Stind_I));
                break;
            default:
                if (elementType.IsValueType)
                {
                    setItemMethod.Body.Instructions.Add(
                        Instruction.Create(OpCodes.Stobj, elementType));
                }
                else
                {
                    setItemMethod.Body.Instructions.Add(
                        Instruction.Create(OpCodes.Stind_Ref));
                }
                break;
        }
        setItemMethod.Body.Instructions.Add(
            Instruction.Create(OpCodes.Ret));

        ///////////////////////////////

        var indexerProperty = new PropertyDefinition(
            "Item",
            PropertyAttributes.None,
            elementType);
        valueArrayType.Properties.Add(indexerProperty);

        indexerProperty.GetMethod = getItemMethod;
        indexerProperty.SetMethod = setItemMethod;

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
                    // "aaa[10]"
                    else
                    {
                        var length = int.Parse(
                            name.Substring(startBracketIndex + 1, name.Length - startBracketIndex - 2),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);

                        // "aaa_len10"
                        return $"{this.GetValueArrayTypeName(elementTypeName)}_len{length}";
                    }
                }
                break;
        }

        return name;
    }

    private TypeReference GetValueArrayType(
        TypeReference elementType, int length)
    {
        var valueArrayTypeName = $"{this.GetValueArrayTypeName(elementType.Name)}_len{length}";
        var valueArrayTypeFullName = elementType.Namespace == "C.type" ?
            valueArrayTypeName : $"{elementType.Namespace}.{valueArrayTypeName}";

        if (!this.TryGetType(valueArrayTypeFullName, out var valueArrayType))
        {
            valueArrayType = this.CreateValueArrayType(
                elementType.Namespace,
                valueArrayTypeName,
                length,
                elementType);
        }
        return valueArrayType;
    }
}
