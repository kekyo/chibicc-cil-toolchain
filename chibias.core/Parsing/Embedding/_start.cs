/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

// Template implementation for startup.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class Program
{
    private static unsafe int main(int argc, sbyte** argv)
    {
        return 0;
    }
    
    public static unsafe int _start()
    {
        var args = Environment.GetCommandLineArgs();
        var path = args[0];
        if (path.EndsWith(".dll"))
        {
            var extension =
                Environment.OSVersion.Platform == PlatformID.Win32NT ? ".exe" : "";
            path = Path.Combine(
                Path.GetDirectoryName(path) ?? "",
                Path.GetFileNameWithoutExtension(path) + extension);
            if (File.Exists(path))
            {
                args[0] = path;
            }
        }
        sbyte** argv = stackalloc sbyte*[args.Length + 1];
        for (var index = 0; index < args.Length; index++)
        {
            var argBytes = Encoding.UTF8.GetBytes(args[index]);
            var argBytes0 = new byte[argBytes.Length + 1];
            Array.Copy(argBytes, argBytes0, argBytes.Length);
            var argHandle = GCHandle.Alloc(argBytes0, GCHandleType.Pinned);
            *(argv + index) = (sbyte*)argHandle.AddrOfPinnedObject();
        }
        return main(args.Length, argv);
    }
}