/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static VerifyNUnit.Verifier;

namespace chibias.core.Tests;

[TestFixture]
public sealed class AssemblerTests
{
    private readonly string id =
        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}";

    private string Run(
        string chibiasSourceCode,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        [CallerMemberName] string memberName = null!)
    {
        var basePath = Path.GetFullPath(
            Path.Combine(
                "tests",
                id,
                memberName));

        Directory.CreateDirectory(basePath);

        var logPath = Path.Combine(basePath, "log.txt");

        var disassembledSourceCode = new StringBuilder();
        using (var logfs = new FileStream(
            logPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            var logtw = new StreamWriter(
                logfs, Encoding.UTF8);
            var logger = new TextWriterLogger(
                LogLevels.Debug, logtw);

            try
            {
                var sourcePath = Path.Combine(
                    basePath, "source.s");
                using (var tw = File.CreateText(sourcePath))
                {
                    tw.Write(chibiasSourceCode);
                    tw.Flush();
                }

                var tmp2Path = Path.GetFullPath("tmp2.dll");
                var tmp2BasePath = Utilities.GetDirectoryPath(tmp2Path);

                var assember = new Assembler(
                    logger,
                    tmp2BasePath);

                var outputAssemblyPath =
                    Path.Combine(basePath, "output.dll");
                var succeeded = assember.Assemble(
                    new[] { sourcePath },
                    outputAssemblyPath,
                    new[] { tmp2Path },
                    assemblyType,
                    DebugSymbolTypes.Embedded,
                    AssembleOptions.Deterministic,
                    new Version(1, 0, 0),
                    "net48");

                var disassembledPath =
                    Path.Combine(basePath, "output.il");

                var psi = new ProcessStartInfo()
                {
                    // Testing expected content is required MS's ILDAsm format,
                    // so unfortunately runs on Windows...
                    FileName = Path.GetFullPath("ildasm.exe"),
                    Arguments = $"-utf8 -out={disassembledPath} {outputAssemblyPath}"
                };

                using (var ildasm = Process.Start(psi)!)
                {
                    ildasm.WaitForExit();
                }

                if (!succeeded)
                {
                    throw new FormatException($"Failed assembling, see {basePath}");
                }

                using var disassembledReader = File.OpenText(disassembledPath);
                while (true)
                {
                    var line = disassembledReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    if (!line.StartsWith("// Image base:"))
                    {
                        disassembledSourceCode.AppendLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
            finally
            {
                logtw.Flush();
            }
        }

        Directory.Delete(basePath, true);

        return disassembledSourceCode.ToString();
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task SimpleOpCodeMainFunction()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Comment()
    {
        var actual = Run(@"
            .function int32 main  ; This is
                ldc.i4.1          ; Ignored.
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Location1()
    {
        var actual = Run(@"
            .function int32 main
                .location 123
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Location2()
    {
        var actual = Run(@"
            .function int32 main
                .location 123 test.c c
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task MultipleFunctions()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.1
                ret
            .function int32 foo
                ldc.i4.2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task SimpleOpCodeMainFunctionInExe()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.1
                ret",
            AssemblyTypes.Exe);
        return Verify(actual);
    }

    [Test]
    public Task SimpleOpCodeMainFunctionInWinExe()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.1
                ret",
            AssemblyTypes.WinExe);
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LdcI4Varies1()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies2()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.s 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies3()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies4()
    {
        var actual = Run($@"
            .function int32 main
                ldc.i4 {int.MaxValue}
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies5()
    {
        var actual = Run($@"
            .function int32 main
                ldc.i4 {int.MinValue}
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies6()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4.s 0x42
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies7()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4 0x12345678
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies8()
    {
        var actual = Run($@"
            .function int32 main
                ldc.i4.s {sbyte.MinValue}
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies9()
    {
        var actual = Run($@"
            .function int32 main
                ldc.i4.s {sbyte.MaxValue}
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LdcI8Varies1()
    {
        var actual = Run($@"
            .function int32 main
                ldc.i8 {long.MaxValue}
                pop
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI8Varies2()
    {
        var actual = Run($@"
            .function int32 main
                ldc.i8 {long.MinValue}
                pop
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LdcR8()
    {
        var actual = Run($@"
            .function int32 main
                ldc.r8 {double.MaxValue}
                pop
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcR8Varies2()
    {
        var actual = Run($@"
            .function int32 main
                ldc.r8 {double.MinValue}
                pop
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LdcR4Varies1()
    {
        var actual = Run($@"
            .function int32 main
                ldc.r4 {float.MaxValue}
                pop
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcR4Varies2()
    {
        var actual = Run($@"
            .function int32 main
                ldc.r4 {float.MinValue}
                pop
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task BrVaries1()
    {
        var actual = Run(@"
            .function int32 main
                br LEND
              LEND:
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BrVaries2()
    {
        var actual = Run(@"
            .function int32 main
                br.s LEND
              LEND:
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BrVaries3()
    {
        var actual = Run(@"
            .function int32 main
                br.s LEND
              LEND:
                ret
            .function int32 foo
                br.s LEND
              LEND:
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LocalVariable1()
    {
        var actual = Run(@"
            .function int32 main
                .local int32
                ldc.i4.1
                stloc.0
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LocalVariable2()
    {
        var actual = Run(@"
            .function int32 main
                .local int32
                .local int32
                ldc.i4.1
                ldc.i4.2
                stloc.0
                stloc.1
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LocalVariable3()
    {
        var actual = Run(@"
            .function int32 main
                .local int32
                ldc.i4.1
                stloc 0
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LocalVariable4()
    {
        var actual = Run(@"
            .function int32 main
                .local int32
                ldc.i4.1
                stloc.s 0
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LocalVariable5()
    {
        var actual = Run(@"
            .function int32 main
                .local int32 abc
                ldc.i4.1
                stloc.0
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task Argument1()
    {
        var actual = Run(@"
            .function int32 foo int32
                ldarg.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument2()
    {
        var actual = Run(@"
            .function int32 foo int32
                ldarg.s 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument3()
    {
        var actual = Run(@"
            .function int32 foo int32
                ldarg 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument4()
    {
        var actual = Run(@"
            .function int32 foo int32 int32
                ldarg.0
                pop
                ldarg.s 1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument5()
    {
        var actual = Run(@"
            .function int32 foo a:int32
                ldarg.s 0
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task AccessCAbiTargetField()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4 123
                stsfld gvalue
                ldsfld gvalue
                ret");
        return Verify(actual);
    }

    [Test]
    public Task AccessSameAssemblyGlobalVariable()
    {
        var actual = Run(@"
            .function int32 main
                ldc.i4 123
                stsfld foo
                ldsfld foo
                ret
            .global int32 foo");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task CallCAbiTargetFunction()
    {
        var actual = Run(@"
            .function int32 main
                call ret3
                ret");
        return Verify(actual);
    }

    [Test]
    public Task CallSameAssemblyFunction()
    {
        var actual = Run(@"
            .function int32 main
                call foo
                ret
            .function int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task UInt8Type()
    {
        var actual = Run(@"
            .function uint8 foo
                ldc.i4.1
                conv.u1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int8Type()
    {
        var actual = Run(@"
            .function int8 foo
                ldc.i4.1
                conv.i1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int16Type()
    {
        var actual = Run(@"
            .function int16 foo
                ldc.i4.1
                conv.i2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task UInt16Type()
    {
        var actual = Run(@"
            .function uint16 foo
                ldc.i4.1
                conv.u2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int32Type()
    {
        var actual = Run(@"
            .function int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task UInt32Type()
    {
        var actual = Run(@"
            .function uint32 foo
                ldc.i4.1
                conv.u4
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int64Type()
    {
        var actual = Run(@"
            .function int64 foo
                ldc.i8 1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task UInt64Type()
    {
        var actual = Run(@"
            .function uint64 foo
                ldc.i8 1
                conv.u8
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Float32Type()
    {
        var actual = Run(@"
            .function float32 foo
                ldc.r4 1.234
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Float64Type()
    {
        var actual = Run(@"
            .function float64 foo
                ldc.r8 1.234
                ret");
        return Verify(actual);
    }

    [Test]
    public Task NativeIntType()
    {
        var actual = Run(@"
            .function intptr foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task NativeUIntType()
    {
        var actual = Run(@"
            .function uintptr foo
                ldc.i4.1
                conv.u
                ret");
        return Verify(actual);
    }

    [Test]
    public Task VoidType()
    {
        var actual = Run(@"
            .function void foo
                ldc.i4.1
                pop
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BoolType()
    {
        var actual = Run(@"
            .function bool foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task CharType()
    {
        var actual = Run(@"
            .function char foo
                ldc.i4.1
                conv.u2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ObjectType()
    {
        var actual = Run(@"
            .function object foo
                ldc.i4.1
                box int32
                ret");
        return Verify(actual);
    }

    [Test]
    public Task StringType()
    {
        var actual = Run(@"
            .function string foo
                ldstr ""abc""
                ret");
        return Verify(actual);
    }

    [Test]
    public Task PointerType()
    {
        var actual = Run(@"
            .function int32* foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task PointerPointerType()
    {
        var actual = Run(@"
            .function int32** foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ByReferenceType()
    {
        var actual = Run(@"
            .function int32& foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ArrayType()
    {
        var actual = Run(@"
            .function int32[] foo
                ldc.i4.4
                newarr int32
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ArrayArrayType()
    {
        var actual = Run(@"
            .function int32[][] foo
                ldc.i4.4
                newarr int32[]
                ret");
        return Verify(actual);
    }
}
