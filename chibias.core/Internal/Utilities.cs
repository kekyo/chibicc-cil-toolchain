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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace chibias.Internal;

internal enum ScopeDescriptors
{
    Public,
    Internal,
    File,
}

internal static class Utilities
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

#if NET40 || NET45
    private static class ArrayEmpty<T>
    {
        public static readonly T[] Empty = new T[0];
    }

    public static T[] Empty<T>() =>
        ArrayEmpty<T>.Empty;
#else
    public static T[] Empty<T>() =>
        Array.Empty<T>();
#endif

    public static string GetDirectoryPath(string path) =>
        Path.GetDirectoryName(path) is { } d ?
            Path.GetFullPath(string.IsNullOrWhiteSpace(d) ? "." : d) :
            Path.DirectorySeparatorChar.ToString();

    public static IEnumerable<TR> Collect<TR, T>(
        this IEnumerable<T> enumerable,
        Func<T, TR?> selector)
        where TR : class
    {
        foreach (var item in enumerable)
        {
            if (selector(item) is { } value)
            {
                yield return value;
            }
        }
    }

    public static IEnumerable<TR> Collect<TR, T>(
        this IEnumerable<T> enumerable,
        Func<T, TR?> selector)
        where TR : struct
    {
        foreach (var item in enumerable)
        {
            if (selector(item) is { } value)
            {
                yield return value;
            }
        }
    }

    public static bool TryParseUInt8(string word, out byte value) =>
        byte.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         byte.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseInt8(string word, out sbyte value) =>
        sbyte.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         sbyte.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseInt16(string word, out short value) =>
        short.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         short.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseUInt16(string word, out ushort value) =>
        ushort.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         ushort.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseInt32(string word, out int value) =>
        int.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         int.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseUInt32(string word, out uint value) =>
        uint.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         uint.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseInt64(string word, out long value) =>
        long.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         long.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseUInt64(string word, out ulong value) =>
        ulong.TryParse(
            word,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value) ||
        (word.StartsWith("0x") &&
         ulong.TryParse(
            word.Substring(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out value));

    public static bool TryParseFloat32(string word, out float value) =>
        float.TryParse(
            word,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    public static bool TryParseFloat64(string word, out double value) =>
        double.TryParse(
            word,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    public static bool TryParseEnum<TEnum>(string word, out TEnum value)
        where TEnum : struct, Enum =>
        Enum.TryParse(word, true, out value);

    public static bool TryParseOpCode(
        string word,
        out OpCode opCode) =>
        opCodes.TryGetValue(word, out opCode);
}
