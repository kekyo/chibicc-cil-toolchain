/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using Mono.Cecil;
using System.Collections.Generic;

namespace chibild.Internal;

internal abstract class EnumerationMemberValueManipulator
{
    private static readonly Dictionary<string, EnumerationMemberValueManipulator> instances = new()
    {
        { "System.Byte", new ByteManipulator() },
        { "System.SByte", new SByteManipulator() },
        { "System.Int16", new Int16Manipulator() },
        { "System.UInt16", new UInt16Manipulator() },
        { "System.Int32", new Int32Manipulator() },
        { "System.UInt32", new UInt32Manipulator() },
        { "System.Int64", new Int64Manipulator() },
        { "System.UInt64", new UInt64Manipulator() },
    };

    protected EnumerationMemberValueManipulator()
    {
    }

    public abstract object GetInitialMemberValue();
    public abstract bool TryParseMemberValue(Token memberValueToken, out object memberValue);
    public abstract object IncrementMemberValue(object memberValue);

    public static EnumerationMemberValueManipulator GetInstance(TypeReference type) =>
        instances[type.FullName];

    private sealed class ByteManipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (byte)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseUInt8(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (byte)(((byte)memberValue) + 1);
    }

    private sealed class SByteManipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (sbyte)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseInt8(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (sbyte)(((sbyte)memberValue) + 1);
    }

    private sealed class Int16Manipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (short)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseInt16(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (short)(((short)memberValue) + 1);
    }

    private sealed class UInt16Manipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (ushort)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseUInt16(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (ushort)(((ushort)memberValue) + 1);
    }

    private sealed class Int32Manipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseInt32(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (int)(((int)memberValue) + 1);
    }

    private sealed class UInt32Manipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (uint)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseUInt32(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (uint)(((uint)memberValue) + 1);
    }

    private sealed class Int64Manipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (long)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseInt64(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (long)(((long)memberValue) + 1);
    }

    private sealed class UInt64Manipulator : EnumerationMemberValueManipulator
    {
        public override object GetInitialMemberValue() =>
            (ulong)0;

        public override bool TryParseMemberValue(Token memberValueToken, out object memberValue)
        {
            if (Utilities.TryParseUInt64(memberValueToken.Text, out var value))
            {
                memberValue = value;
                return true;
            }
            else
            {
                memberValue = null!;
                return false;
            }
        }

        public override object IncrementMemberValue(object memberValue) =>
            (ulong)(((ulong)memberValue) + 1);
    }
}
