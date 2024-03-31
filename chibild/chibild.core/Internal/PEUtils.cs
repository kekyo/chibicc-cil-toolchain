/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

// Imported from:
// https://github.com/dotnet/runtime/blob/release/5.0/src/installer/managed/Microsoft.NET.HostModel/AppHost/PEUtils.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace chibild.Internal;

internal static class PEUtils
{
    /// <summary>
    /// The first two bytes of a PE file are a constant signature.
    /// </summary>
    private const UInt16 PEFileSignature = 0x5A4D;

    /// <summary>
    /// The offset of the PE header pointer in the DOS header.
    /// </summary>
    private const int PEHeaderPointerOffset = 0x3C;

    /// <summary>
    /// The offset of the Subsystem field in the PE header.
    /// </summary>
    private const int SubsystemOffset = 0x5C;

    /// <summary>
    /// The value of the sybsystem field which indicates Windows GUI (Graphical UI)
    /// </summary>
    private const UInt16 WindowsGUISubsystem = 0x2;

    /// <summary>
    /// The value of the subsystem field which indicates Windows CUI (Console)
    /// </summary>
    private const UInt16 WindowsCUISubsystem = 0x3;

    /// <summary>
    /// Check whether the apphost file is a windows PE image by looking at the first few bytes.
    /// </summary>
    /// <param name="ms">The memory accessor which has the apphost file opened.</param>
    /// <returns>true if the accessor represents a PE image, false otherwise.</returns>
    internal static unsafe bool IsPEImage(MemoryStream ms)
    {
        var buffer = ms.GetBuffer();
        fixed (byte* bytes = &buffer[0])
        {
            // https://en.wikipedia.org/wiki/Portable_Executable
            // Validate that we're looking at Windows PE file
            if (((UInt16*)bytes)[0] != PEFileSignature || ms.Length < PEHeaderPointerOffset + sizeof(UInt32))
            {
                return false;
            }
            return true;
        }
    }

    public static bool IsPEImage(string filePath)
    {
        using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
        {
            if (reader.BaseStream.Length < PEHeaderPointerOffset + sizeof(UInt32))
            {
                return false;
            }

            ushort signature = reader.ReadUInt16();
            return signature == PEFileSignature;
        }
    }

    /// <summary>
    /// This method will attempt to set the subsystem to GUI. The apphost file should be a windows PE file.
    /// </summary>
    /// <param name="ms">The memory accessor which has the apphost file opened.</param>
    internal static unsafe void SetWindowsGraphicalUserInterfaceBit(MemoryStream ms)
    {
        var buffer = ms.GetBuffer();
        fixed (byte* bytes = &buffer[0])
        {
            // https://en.wikipedia.org/wiki/Portable_Executable
            UInt32 peHeaderOffset = ((UInt32*)(bytes + PEHeaderPointerOffset))[0];

            if (ms.Length < peHeaderOffset + SubsystemOffset + sizeof(UInt16))
            {
                throw new FormatException("AppHost is not PE format.");
            }

            UInt16* subsystem = ((UInt16*)(bytes + peHeaderOffset + SubsystemOffset));

            // https://docs.microsoft.com/en-us/windows/desktop/Debug/pe-format#windows-subsystem
            // The subsystem of the prebuilt apphost should be set to CUI
            if (subsystem[0] != WindowsCUISubsystem)
            {
                throw new FormatException("AppHost is not CUI.");
            }

            // Set the subsystem to GUI
            subsystem[0] = WindowsGUISubsystem;
        }
    }

    /// <summary>
    /// This method will return the subsystem CUI/GUI value. The apphost file should be a windows PE file.
    /// </summary>
    /// <param name="ms">The memory accessor which has the apphost file opened.</param>
    internal static unsafe UInt16 GetWindowsGraphicalUserInterfaceBit(MemoryStream ms)
    {
        var buffer = ms.GetBuffer();
        fixed (byte* bytes = &buffer[0])
        {
            // https://en.wikipedia.org/wiki/Portable_Executable
            UInt32 peHeaderOffset = ((UInt32*)(bytes + PEHeaderPointerOffset))[0];

            if (ms.Length < peHeaderOffset + SubsystemOffset + sizeof(UInt16))
            {
                throw new FormatException("AppHost is not PE format.");
            }

            UInt16* subsystem = ((UInt16*)(bytes + peHeaderOffset + SubsystemOffset));

            return subsystem[0];
        }
    }
}