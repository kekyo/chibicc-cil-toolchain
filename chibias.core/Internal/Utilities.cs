/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace chibias.Internal;

[Flags]
internal enum chmodFlags
{
    S_IRUSR = 0x100,
    S_IWUSR = 0x80,
    S_IXUSR = 0x40,
    S_IRGRP = 0x20,
    S_IWGRP = 0x10,
    S_IXGRP = 0x8,
    S_IROTH = 0x4,
    S_IWOTH = 0x2,
    S_IXOTH = 0x1,
}

internal static class Utilities
{
    public static readonly bool IsInWindows =
        Environment.OSVersion.Platform == PlatformID.Win32NT;

    public const int EINTR = 4;
    
    [DllImport("libc", SetLastError = true)]
    public static extern int chmod(string path, chmodFlags mode);

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

    public static string IntersectStrings(IEnumerable<string> strings)
    {
        var sb = new StringBuilder();
        foreach (var str in strings)
        {
            if (sb.Length == 0)
            {
                sb.Append(str);
            }
            else
            {
                var length = Math.Min(sb.Length, str.Length);
                var index = 0;
                while (index < length)
                {
                    if (str[index] != sb[index])
                    {
                        sb.Remove(index, sb.Length - index);
                        break;
                    }
                    index++;
                }
            }
        }
        return sb.ToString();
    }

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
    
    public static bool UpdateBytes(
        MemoryStream ms,
        byte[] targetBytes, byte[] replaceBytes)
    {
        var data = ms.GetBuffer();
        var index = 0;
        while (index < ms.Length)
        {
            var targetIndex = 0;
            while (targetIndex < targetBytes.Length)
            {
                if (data[index + targetIndex] != targetBytes[targetIndex])
                {
                    break;
                }
                targetIndex++;
            }
            if (targetIndex >= targetBytes.Length)
            {
                Array.Copy(replaceBytes, 0, data, index, replaceBytes.Length);
                for (var index2 = replaceBytes.Length; index2 < targetBytes.Length; index2++)
                {
                    data[index + index2] = 0x00;
                }
                return true;
            }
            index++;
        }
        return false;
    }
}
