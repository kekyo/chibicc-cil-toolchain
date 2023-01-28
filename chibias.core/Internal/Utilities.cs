/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace chibias.Internal;

internal static class Utilities
{
    private static readonly Dictionary<string, OpCode> opCodes =
        typeof(OpCodes).GetFields().
        Where(field =>
            field.IsPublic && field.IsStatic && field.IsInitOnly &&
            field.FieldType.FullName == "Mono.Cecil.Cil.OpCode").
        Select(field => (OpCode)field.GetValue(null)!).
        ToDictionary(opCode => opCode.Name.Replace('_', '.').ToLowerInvariant());

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
