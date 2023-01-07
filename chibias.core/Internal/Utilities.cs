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

    public static readonly char[] Separators = new[] { ' ', '\t' };

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
