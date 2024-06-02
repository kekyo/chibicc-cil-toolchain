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
    private static readonly Dictionary<string, OpCode> opCodes =
        CecilDefinition.GetOpCodes();

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

#if DEBUG
    static CecilUtilities()
    {
        var translator = CilParser.GetOpCodeTranslator();
        Debug.Assert(translator.All(t => opCodes.ContainsKey(t.Value)));
    }
#endif

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
