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
        TypeReference elementType,
        bool isReadOnly)
    {
        var valueArrayType = new TypeDefinition(
            valueArrayTypeNamespace,
            valueArrayTypeName,
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            this.valueType.Value);
        //valueArrayType.PackingSize = 1;
        //valueArrayType.ClassSize = 0;
        this.module.Types.Add(valueArrayType);

        ///////////////////////////////

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
                new(this.UnsafeGetType("System.String"), "Item"));

            valueArrayType.CustomAttributes.Add(defaultMemberCustomAttribute);
        }

        ///////////////////////////////

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

        indexerProperty.GetMethod = getItemMethod;

        ///////////////////////////////

        var getItemInstructions = getItemMethod.Body.Instructions;

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

        // Body
        getItemInstructions.Add(getItemNext);
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Sizeof, elementType));
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Mul));
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Ldarg_0));
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Conv_U));
        getItemInstructions.Add(
            Instruction.Create(OpCodes.Add));
        switch (elementType.FullName)
        {
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
            setItemMethod.Parameters.Add(new(
                "value",
                ParameterAttributes.None,
                elementType));
            valueArrayType.Methods.Add(setItemMethod);

            indexerProperty.SetMethod = setItemMethod;

            ///////////////////////////////

            var setItemInstructions = setItemMethod.Body.Instructions;

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

            // Body
            setItemInstructions.Add(setItemNext);
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Sizeof, elementType));
            setItemInstructions.Add(
                Instruction.Create(OpCodes.Mul));
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
                case "System.Byte":
                case "System.SByte":
                    setItemInstructions.Add(
                        Instruction.Create(OpCodes.Stind_I1));
                    break;
                case "System.Int16":
                case "System.UInt16":
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
                elementType,
                false);
        }
        return valueArrayType;
    }
}
