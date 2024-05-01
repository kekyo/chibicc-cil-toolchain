/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using chibicc.toolchain.Parsing;

namespace chibild.Internal;

internal static class CecilUtilities
{
    private static readonly Dictionary<string, OpCode> opCodes;

    private static readonly HashSet<char> invalidMemberNameChars = new()
    {
        '-', '+', '=', '#', '@', '$', '%', '~', '.', ',', ':', ';',
        '*', '&', '^', '?', '!', '\'', '"', '`', '|', '/', '\\',
        '[', ']', '(', ')', '<', '>', '{', '}',
        '\a', '\b', '\t', '\n', '\v', '\f', '\r',
        '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u000e', '\u000f',
        '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017',
        '\u0018', '\u0019', '\u001a', '\u001b', '\u001c', '\u001d', '\u001e', '\u001f',
    };

    static CecilUtilities()
    {
        var translator = CilParser.GetOpCodeTranslator();

        opCodes = typeof(OpCodes).GetFields().
            Where(field =>
                field.IsPublic && field.IsStatic && field.IsInitOnly &&
                field.FieldType.FullName == "Mono.Cecil.Cil.OpCode").
            Select(field =>
            {
                var opCode = (OpCode)field.GetValue(null)!;

                // Verify between Cecil's opcode and Reflection.Emit opcode.
                if (!translator.TryGetValue(opCode.Value, out var name))
                {
                    name = null;
                }

                return (name, opCode);
            }).
            Where(entry => entry.name != null && entry.opCode.OpCodeType != OpCodeType.Nternal).
            ToDictionary(
                entry => entry.name!,
                entry => entry.opCode,
                StringComparer.OrdinalIgnoreCase);

        Debug.Assert(translator.All(t => opCodes.ContainsKey(t.Value)));
    }

    public static int GetOpCodeStackSize(StackBehaviour sb) =>
        sb switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 => -1,
            StackBehaviour.Pop1_pop1 => -2,
            StackBehaviour.Popi => -1,
            StackBehaviour.Popi_pop1 => -2,
            StackBehaviour.Popi_popi => -2,
            StackBehaviour.Popi_popi8 => -2,
            StackBehaviour.Popi_popi_popi => -3,
            StackBehaviour.Popi_popr4 => -2,
            StackBehaviour.Popi_popr8 => -2,
            StackBehaviour.Popref => -1,
            StackBehaviour.Popref_pop1 => -2,
            StackBehaviour.Popref_popi => -2,
            StackBehaviour.Popref_popi_popi => -3,
            StackBehaviour.Popref_popi_popi8 => -3,
            StackBehaviour.Popref_popi_popr4 => -3,
            StackBehaviour.Popref_popi_popr8 => -3,
            StackBehaviour.Popref_popi_popref => -3,
            StackBehaviour.Varpop => -1,
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 => 1,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Pushi => 1,
            StackBehaviour.Pushi8 => 1,
            StackBehaviour.Pushr4 => 1,
            StackBehaviour.Pushr8 => 1,
            StackBehaviour.Pushref => 1,
            StackBehaviour.Varpush => 1,
            _ => 0,
        };

    public static Instruction CreateInstruction(OpCode opCode, object operand) =>
        operand switch
        {
            MethodReference method => Instruction.Create(opCode, method),
            FieldReference field => Instruction.Create(opCode, field),
            TypeReference type => Instruction.Create(opCode, type),
            CallSite callSite => Instruction.Create(opCode, callSite),
            Instruction instruction => Instruction.Create(opCode, instruction),
            _ => throw new InvalidOperationException(),
        };

    public static string SanitizeFileNameToMemberName(string fileName)
    {
        var sb = new StringBuilder(fileName);
        for (var index = 0; index < sb.Length; index++)
        {
            if (invalidMemberNameChars.Contains(sb[index]))
            {
                sb[index] = '_';
            }
        }
        if (fileName.EndsWith(".s") || fileName.EndsWith(".o"))
        {
            sb.Remove(sb.Length - 2, 2);
        }
        return sb.ToString();
    }

    public static OpCode ParseOpCode(
        string word) =>
        opCodes[word];

    public static bool TryMakeFunctionPointerType(
        this MethodReference method,
        out FunctionPointerType type)
    {
        if (method.HasThis)
        {
            type = null!;
            return false;
        }

        type = new FunctionPointerType
        {
            ReturnType = method.ReturnType,
            CallingConvention = method.CallingConvention,
            HasThis = method.HasThis,
            ExplicitThis = method.ExplicitThis,
        };
        foreach (var parameter in method.Parameters)
        {
            type.Parameters.Add(new(
                parameter.Name, parameter.Attributes, parameter.ParameterType));
        }

        return true;
    }

    public static TypeDefinition CreatePlaceholderType(int postfix) =>
        new("", $"<placeholder_type>_${postfix}",
            TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);

    public static FieldDefinition CreatePlaceholderField(int postfix) =>
        new($"<placeholder_field>_${postfix}",
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly,
            CreatePlaceholderType(postfix));

    public static MethodDefinition CreatePlaceholderMethod(int postfix) =>
        new($"<placeholder_method>_${postfix}",
            MethodAttributes.Private | MethodAttributes.Abstract | MethodAttributes.Final,
            CreatePlaceholderType(postfix));

    public static Instruction CreatePlaceholderInstruction(int postfix) =>
        Instruction.Create(OpCodes.Ldc_I4, postfix);

    private sealed class TypeReferenceComparer :
        IEqualityComparer<TypeReference>
    {
        public bool Equals(TypeReference? lhs, TypeReference? rhs) =>
            lhs is { } && rhs is { } &&
            lhs.FullName.Equals(rhs.FullName);

        public int GetHashCode(TypeReference obj) =>
            obj.FullName.GetHashCode();

        public static readonly TypeReferenceComparer Instance = new();
    }

    public static bool Equals(
        TypeReference lhs,
        TypeReference rhs) =>
        TypeReferenceComparer.Instance.Equals(lhs, rhs);

    private sealed class ParameterReferenceComparer :
        IEqualityComparer<ParameterReference>
    {
        public bool Equals(ParameterReference? lhs, ParameterReference? rhs) =>
            lhs is { } && rhs is { } &&
            lhs.ParameterType.FullName.Equals(rhs.ParameterType.FullName);

        public int GetHashCode(ParameterReference obj) =>
            obj.ParameterType.FullName.GetHashCode();

        public static readonly ParameterReferenceComparer Instance = new();
    }

    public static bool Equals(
        MethodReference lhs,
        MethodReference rhs) =>
        lhs.Name == rhs.Name &&
        lhs.ReturnType.FullName == rhs.ReturnType.FullName &&
        (lhs.CallingConvention, rhs.CallingConvention) switch
        {
            (Mono.Cecil.MethodCallingConvention.VarArg, Mono.Cecil.MethodCallingConvention.VarArg) =>
                lhs.Parameters.
                    Take(Math.Min(lhs.Parameters.Count, rhs.Parameters.Count)).
                    SequenceEqual(rhs.Parameters.
                        Take(Math.Min(lhs.Parameters.Count, rhs.Parameters.Count)),
                        ParameterReferenceComparer.Instance),
            (Mono.Cecil.MethodCallingConvention.VarArg, _) when
                lhs.Parameters.Count <= rhs.Parameters.Count =>
                lhs.Parameters.
                    SequenceEqual(rhs.Parameters.
                        Take(lhs.Parameters.Count),
                        ParameterReferenceComparer.Instance),
            (_, Mono.Cecil.MethodCallingConvention.VarArg) when
                rhs.Parameters.Count <= lhs.Parameters.Count =>
                rhs.Parameters.
                    SequenceEqual(lhs.Parameters.
                        Take(rhs.Parameters.Count),
                        ParameterReferenceComparer.Instance),
            _ => lhs.Parameters.
                SequenceEqual(rhs.Parameters,
                    ParameterReferenceComparer.Instance),
        };

    public static bool IsValidCAbiParameter(
        MethodReference method, string[] parameterTypeNames) =>
        method.CallingConvention switch
        {
            Mono.Cecil.MethodCallingConvention.VarArg =>
                method.Parameters.
                    Zip(parameterTypeNames, (p, ptn) => p.ParameterType.FullName == ptn).
                    All(eq => eq),
            _ =>
                parameterTypeNames.Length == 0,
        };
    
    public static void SetFieldType(
        FieldDefinition field, TypeReference type)
    {
        field.FieldType = type;

        // Special case: Force 1 byte footprint on boolean type.
        if (type.FullName == "System.Boolean")
        {
            field.MarshalInfo = new(NativeType.U1);
        }
        else if (type.FullName == "System.Char")
        {
            field.MarshalInfo = new(NativeType.U2);
        }
    }

    public static TypeReference SafeImport(
        this ModuleDefinition targetModule,
        TypeReference tr) =>
        (tr.Module?.Equals(targetModule) ?? tr is TypeDefinition) ?
            tr : targetModule.ImportReference(tr);
        
    public static FieldReference SafeImport(
        this ModuleDefinition targetModule,
        FieldReference fr) =>
        (fr.Module?.Equals(targetModule) ?? fr is FieldDefinition) ?
            fr : targetModule.ImportReference(fr);
        
    public static MethodReference SafeImport(
        this ModuleDefinition targetModule,
        MethodReference mr) =>
        (mr.Module?.Equals(targetModule) ?? mr is MethodDefinition) ?
            mr : targetModule.ImportReference(mr);

    public static MemberReference SafeImport(
        this ModuleDefinition targetModule,
        MemberReference member) =>
        member switch
        {
            TypeReference type => targetModule.SafeImport(type),
            FieldReference field => targetModule.SafeImport(field),
            MethodReference method => targetModule.SafeImport(method),
            _ => throw new InvalidOperationException(),
        };
}
