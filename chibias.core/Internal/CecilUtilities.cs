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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace chibias.Internal;

internal enum ScopeDescriptors
{
    Public,
    Internal,
    File,
}

internal static class CecilUtilities
{
    private static readonly Dictionary<string, OpCode> opCodes =
        typeof(OpCodes).GetFields().
        Where(field =>
            field.IsPublic && field.IsStatic && field.IsInitOnly &&
            field.FieldType.FullName == "Mono.Cecil.Cil.OpCode").
        Select(field => (OpCode)field.GetValue(null)!).
        ToDictionary(opCode => opCode.Name.Replace('_', '.').ToLowerInvariant());

    private static readonly Dictionary<string, ScopeDescriptors> scopeDescriptors = new()
    {
        { "public", ScopeDescriptors.Public },
        { "internal", ScopeDescriptors.Internal },
        { "file", ScopeDescriptors.File },
    };

    private static readonly Dictionary<string, string> aliasTypeNames =
        new Dictionary<string, string>()
    {
        { "void", "System.Void" },
        { "uint8", "System.Byte" },
        { "int8", "System.SByte" },
        { "int16", "System.Int16" },
        { "uint16", "System.UInt16" },
        { "int32", "System.Int32" },
        { "uint32", "System.UInt32" },
        { "int64", "System.Int64" },
        { "uint64", "System.UInt64" },
        { "float32", "System.Single" },
        { "float64", "System.Double" },
        { "intptr", "System.IntPtr" },
        { "uintptr", "System.UIntPtr" },
        { "bool", "System.Boolean" },
        { "char", "System.Char" },
        { "object", "System.Object" },
        { "string", "System.String" },
        { "typeref", "System.TypedReference" },
        { "byte", "System.Byte" },
        { "sbyte", "System.SByte" },
        { "short", "System.Int16" },
        { "ushort", "System.UInt16" },
        { "int", "System.Int32" },
        { "uint", "System.UInt32" },
        { "long", "System.Int64" },
        { "ulong", "System.UInt64" },
        { "single", "System.Single" },
        { "float", "System.Single" },
        { "double", "System.Double" },
        { "nint", "System.IntPtr" },
        { "nuint", "System.UIntPtr" },
        { "char16", "System.Char" },
    };

    private static readonly HashSet<string> enumerationUnderlyingTypes = new HashSet<string>()
    {
        "System.Byte", "System.SByte", "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64",
    };

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
        if (fileName.EndsWith(".s"))
        {
            sb.Remove(sb.Length - 2, 2);
        }
        return sb.ToString();
    }

    public static bool TryLookupScopeDescriptorName(
        string scopeDescriptorName,
        out ScopeDescriptors scopeDescriptor) =>
        scopeDescriptors.TryGetValue(scopeDescriptorName, out scopeDescriptor);

    public static bool TryLookupOriginTypeName(
        string typeName,
        out string originTypeName) =>
        aliasTypeNames.TryGetValue(typeName, out originTypeName!);

    public static bool IsEnumerationUnderlyingType(
        string typeName) =>
        enumerationUnderlyingTypes.Contains(typeName);

    public static bool TryParseOpCode(
        string word,
        out OpCode opCode) =>
        opCodes.TryGetValue(word, out opCode);
}
