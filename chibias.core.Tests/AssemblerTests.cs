/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using NUnit.Framework;
using System;
using System.Threading.Tasks;

using static VerifyNUnit.Verifier;

namespace chibias.core.Tests;

[TestFixture]
public sealed partial class AssemblerTests
{
    [Test]
    public Task SimpleOpCodeMainFunction()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Comment()
    {
        var actual = Run(@"
            .function public int32 main  ; This is
                ldc.i4.1          ; Ignored.
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Location1()
    {
        var actual = Run(@"
            .file 1 ""abc.c"" c
            .function public int32 main
                .location 1 123 8 123 20
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Location2()
    {
        var actual = Run(@"
            .file 1 ""abc.c"" c
            .function public int32 main
                .location 1 123 8 123 20
                ldc.i4.1
                ldc.i4.2
                add
                .location 1 124 8 124 12
                ldc.i4.6
                sub
                ret");
        return Verify(actual);
    }

    [Test]
    public Task MultipleFunctions()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret
            .function public int32 foo
                ldc.i4.2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task SimpleOpCodeMainFunctionInExe()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
            AssemblyTypes.Exe);
        return Verify(actual);
    }

    [Test]
    public Task SimpleOpCodeMainFunctionInWinExe()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
            AssemblyTypes.WinExe);
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task VariableArgumentsFunction()
    {
        var actual = Run(@"
            .function internal void foo ...
                .local System.ArgIterator va
                ldloca.s 0
                arglist
                call System.ArgIterator..ctor System.RuntimeArgumentHandle
                ret");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeFunction()
    {
        var actual = Run(@"
            .function internal int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeFunction()
    {
        var actual = Run(@"
            .function file int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeFunctionReference1()
    {
        var actual = Run(@"
            .function public int32 main
                call foo
                ret
            .function internal int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeFunctionReference2()
    {
        var actual = Run(@"
            .function internal int32 main
                call foo
                ret
            .function internal int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeFunctionReference3()
    {
        var actual = Run(@"
            .function file int32 main
                call foo
                ret
            .function internal int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeFunctionReference1()
    {
        var actual = Run(@"
            .function public int32 main
                call foo
                ret
            .function file int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeFunctionReference2()
    {
        var actual = Run(@"
            .function internal int32 main
                call foo
                ret
            .function file int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeFunctionReference3()
    {
        var actual = Run(@"
            .function file int32 main
                call foo
                ret
            .function file int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeMainFunction()
    {
        var actual = Run(@"
            .function internal int32 main
                ldc.i4.1
                ret",
                AssemblyTypes.Exe);
        return Verify(actual);
    }

    [Test]
    public Task CombinedFunctionScopeVaries()
    {
        var actual = Run(@"
            .function public int32 foo
                ldc.i4.1
                ret
            .function internal int32 bar
                ldc.i4.1
                ret
            .function file int32 baz
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task MainFunctionWithoutReturn()
    {
        var actual = Run(@"
            .function internal void main
                ret",
                AssemblyTypes.Exe);
        return Verify(actual);
    }

    [Test]
    public Task MainFunctionWithReturnAndParameters()
    {
        var actual = Run(@"
            .function internal int32 main argc:int32 argv:int8**
                ldc.i4.1
                ret",
                AssemblyTypes.Exe);
        return Verify(actual);
    }

    [Test]
    public Task MainFunctionWithoutReturnAndParameters()
    {
        var actual = Run(@"
            .function internal void main argc:int32 argv:int8**
                ret",
                AssemblyTypes.Exe);
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task InternalScopeVariable()
    {
        var actual = Run(@"
            .function public int32 main
                ldsfld foo
                ret
            .global internal int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeVariable()
    {
        var actual = Run(@"
            .function public int32 main
                ldsfld foo
                ret
            .global file int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeVariableReference1()
    {
        var actual = Run(@"
            .function internal int32 main
                ldsfld foo
                ret
            .global internal int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeVariableReference2()
    {
        var actual = Run(@"
            .function internal int32 main
                ldsfld foo
                ret
            .global file int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeVariableReference1()
    {
        var actual = Run(@"
            .function file int32 main
                ldsfld foo
                ret
            .global internal int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeVariableReference2()
    {
        var actual = Run(@"
            .function file int32 main
                ldsfld foo
                ret
            .global file int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task CombinedVariableScopeVaries()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret
            .global public int32 foo
            .global internal int32 bar
            .global file int32 baz");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task StringLiteral1()
    {
        var actual = Run(@"
            .function public string foo
                ldstr ""abc""
                ret");
        return Verify(actual);
    }

    [Test]
    public Task StringLiteral2()
    {
        var actual = Run(@"
            .function public string foo
                ldstr ""abc\adef\bghi\fjkl\nmno\rpqr\tstu\vvwx\""yzA\x7fBCD\u12abEFG""
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LdcI4Varies1()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies2()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.s 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies3()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies4()
    {
        var actual = Run($@"
            .function public int32 main
                ldc.i4 {int.MaxValue}
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies5()
    {
        var actual = Run($@"
            .function public int32 main
                ldc.i4 {int.MinValue}
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies6()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.s 0x42
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies7()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4 0x12345678
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies8()
    {
        var actual = Run($@"
            .function public int32 main
                ldc.i4.s {sbyte.MinValue}
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LdcI4Varies9()
    {
        var actual = Run($@"
            .function public int32 main
                ldc.i4.s {sbyte.MaxValue}
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LdcI8Varies1()
    {
        var actual = Run($@"
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
                br LEND
              LEND:
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BrVaries2()
    {
        var actual = Run(@"
            .function public int32 main
                br.s LEND
              LEND:
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BrVaries3()
    {
        var actual = Run(@"
            .function public int32 main
                br.s LEND
              LEND:
                ret
            .function public int32 foo
                br.s LEND
              LEND:
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BrVaries4()
    {
        var actual = Run(@"
            .function public int32 main
                br.s LEND1
              LEND1:
                ldc.i4.1
                br.s LEND2
              LEND2:
                ret
            .function public int32 foo
                br.s LEND1
              LEND1:
                ldc.i4.1
                br.s LEND2
              LEND2:
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task LocalVariable1()
    {
        var actual = Run(@"
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
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
            .function public int32 main
                .local int32 abc
                ldc.i4.1
                stloc.0
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task LocalVariable6()
    {
        var actual = Run(@"
            .function public int32 main
                .local int32 abc
                ldc.i4.1
                stloc abc
                ldc.i4.0
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task Argument1()
    {
        var actual = Run(@"
            .function public int32 foo int32
                ldarg.0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument2()
    {
        var actual = Run(@"
            .function public int32 foo int32
                ldarg.s 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument3()
    {
        var actual = Run(@"
            .function public int32 foo int32
                ldarg 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument4()
    {
        var actual = Run(@"
            .function public int32 foo int32 int32
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
            .function public int32 foo a:int32
                ldarg.s 0
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Argument6()
    {
        var actual = Run(@"
            .function public int32 foo abc:int32
                ldarg abc
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task AccessCAbiTargetField()
    {
        var actual = Run(@"
            .function public int32 main
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
            .function public int32 main
                ldc.i4 123
                stsfld foo
                ldsfld foo
                ret
            .global public int32 foo");
        return Verify(actual);
    }

    [Test]
    public Task AccessExternalAssemblyField()
    {
        var actual = Run(@"
            .function public int32 main
                ldsfld System.Int32.MaxValue
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task CallCAbiTargetFunction()
    {
        var actual = Run(@"
            .function public int32 main
                call ret3
                ret");
        return Verify(actual);
    }

    [Test]
    public Task CallSameAssemblyFunction()
    {
        var actual = Run(@"
            .function public int32 main
                call foo
                ret
            .function public int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }


    /////////////////////////////////////////////////////////

    [Test]
    public Task InitializerOnPublic()
    {
        var actual = Run(@"
            .initializer public
                ldc.i4.2
                stsfld foo
                ret
            .initializer public
                ldc.i4.4
                stsfld bar
                ret
            .global public int32 foo
            .global public int32 bar");
        return Verify(actual);
    }

    [Test]
    public Task InitializerOnInternal()
    {
        var actual = Run(@"
            .initializer internal
                ldc.i4.2
                stsfld foo
                ret
            .initializer internal
                ldc.i4.4
                stsfld bar
                ret
            .global public int32 foo
            .global public int32 bar");
        return Verify(actual);
    }

    [Test]
    public Task InitializerOnFile()
    {
        var actual = Run(@"
            .initializer file
                ldc.i4.2
                stsfld foo
                ret
            .initializer file
                ldc.i4.4
                stsfld bar
                ret
            .global file int32 foo
            .global file int32 bar");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task UInt8Type()
    {
        var actual = Run(@"
            .function public uint8 foo
                ldc.i4.1
                conv.u1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int8Type()
    {
        var actual = Run(@"
            .function public int8 foo
                ldc.i4.1
                conv.i1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int16Type()
    {
        var actual = Run(@"
            .function public int16 foo
                ldc.i4.1
                conv.i2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task UInt16Type()
    {
        var actual = Run(@"
            .function public uint16 foo
                ldc.i4.1
                conv.u2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int32Type()
    {
        var actual = Run(@"
            .function public int32 foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task UInt32Type()
    {
        var actual = Run(@"
            .function public uint32 foo
                ldc.i4.1
                conv.u4
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Int64Type()
    {
        var actual = Run(@"
            .function public int64 foo
                ldc.i8 1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task UInt64Type()
    {
        var actual = Run(@"
            .function public uint64 foo
                ldc.i8 1
                conv.u8
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Float32Type()
    {
        var actual = Run(@"
            .function public float32 foo
                ldc.r4 1.234
                ret");
        return Verify(actual);
    }

    [Test]
    public Task Float64Type()
    {
        var actual = Run(@"
            .function public float64 foo
                ldc.r8 1.234
                ret");
        return Verify(actual);
    }

    [Test]
    public Task NativeIntType()
    {
        var actual = Run(@"
            .function public intptr foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task NativeUIntType()
    {
        var actual = Run(@"
            .function public uintptr foo
                ldc.i4.1
                conv.u
                ret");
        return Verify(actual);
    }

    [Test]
    public Task VoidType()
    {
        var actual = Run(@"
            .function public void foo
                ldc.i4.1
                pop
                ret");
        return Verify(actual);
    }

    [Test]
    public Task BoolType()
    {
        var actual = Run(@"
            .function public void foo
                .local bool a
                ret");
        return Verify(actual);
    }

    [Test]
    public Task CharType()
    {
        var actual = Run(@"
            .function public char foo
                ldc.i4.1
                conv.u2
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ObjectType()
    {
        var actual = Run(@"
            .function public object foo
                ldc.i4.1
                box int32
                ret");
        return Verify(actual);
    }

    [Test]
    public Task StringType()
    {
        var actual = Run(@"
            .function public string foo
                ldstr ""abc""
                ret");
        return Verify(actual);
    }

    [Test]
    public Task PointerType()
    {
        var actual = Run(@"
            .function public int32* foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task PointerPointerType()
    {
        var actual = Run(@"
            .function public int32** foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ByReferenceType()
    {
        var actual = Run(@"
            .function public int32& foo
                ldc.i4.1
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ArrayType()
    {
        var actual = Run(@"
            .function public int32[] foo
                ldc.i4.4
                newarr int32
                ret");
        return Verify(actual);
    }

    [Test]
    public Task ArrayArrayType()
    {
        var actual = Run(@"
            .function public int32[][] foo
                ldc.i4.4
                newarr int32[]
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FunctionPointerType()
    {
        var actual = Run(@"
            .function public string(int32,int8&)* foo
                ldftn bar
                ret
            .function file string bar a:int32 b:int8&
                ldstr ""ABC""
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task CallDotNetAssemblyMethod()
    {
        var actual = Run(@"
            .function public void main
                ldstr ""Hello world""
                call System.Console.WriteLine string
                ret",
            AssemblyTypes.Exe,
            new[] { typeof(System.Console).Assembly.Location });
        return Verify(actual);
    }

    [Test]
    public Task CallDotNetAssemblyMethod2()
    {
        var actual = Run(@"
            .function public void main
                ldc.i4.1
                box int32
                ldc.i4.3
                call System.Runtime.InteropServices.GCHandle.Alloc object System.Runtime.InteropServices.GCHandleType
                pop
                ret",
            AssemblyTypes.Exe,
            new[] { typeof(System.Console).Assembly.Location });
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task SizeOfByType()
    {
        var actual = Run(@"
            .function public int32 foo
                sizeof int32
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task GlobalVariableWithInitializingData1()
    {
        var actual = Run(@"
            .function public int32 foo
                ldsfld bar
                ret
            .global public int32 bar 0x10 0x32 0x54 0x76");
        return Verify(actual);
    }

    [Test]
    public Task GlobalVariableWithInitializingData2()
    {
        var actual = Run(@"
            .function public uint8[6] foo
                ldsfld bar
                ret
            .global public uint8[6] bar 0x01 0x02 0x31 0x32 0xb1 0xb2");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task GlobalVariableToken()
    {
        var actual = Run(@"
            .function public intptr foo
                ldtoken bar
                conv.i
                ret
            .global public int32 bar");
        return Verify(actual);
    }

    [Test]
    public Task FieldToken()
    {
        var actual = Run(@"
            .function public intptr foo
                ldtoken System.Int32.MaxValue
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FunctionToken()
    {
        var actual = Run(@"
            .function public intptr foo
                ldtoken bar
                conv.i
                ret
            .function public int32 bar
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task MethodToken()
    {
        var actual = Run(@"
            .function public intptr foo
                ldtoken System.Int32.Parse string
                conv.i
                ret");
        return Verify(actual);
    }

    [Test]
    public Task TypeToken()
    {
        var actual = Run(@"
            .function public intptr foo
                ldtoken System.Int32
                conv.i
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task CallIndirectWithSignature()
    {
        var actual = Run(@"
            .function public int32 main
                ldstr ""123""
                ldftn System.Int32.Parse string
                calli int32(string)
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task ValueArray1()
    {
        var actual = Run(@"
            .function public int8[6] foo
                ldsfld bar
                ret
            .global public int8[6] bar 0x01 0x02 0x31 0x32 0xb1 0xb2");
        return Verify(actual);
    }

    [Test]
    public Task ValueArray2()
    {
        var actual = Run(@"
            .function public uint8[6]* foo
                ldsfld bar
                ret
            .global public uint8[6]* bar");
        return Verify(actual);
    }

    [Test]
    public Task ValueArray3()
    {
        var actual = Run(@"
            .function public uint8*[6] foo
                ldsfld bar
                ret
            .global public uint8*[6] bar");
        return Verify(actual);
    }

    [Test]
    public Task ValueArray4()
    {
        var actual = Run(@"
            .function public uint8&[6] foo
                ldsfld bar
                ret
            .global public uint8&[6] bar");
        return Verify(actual);
    }

    [Test]
    public Task ValueArray5()
    {
        var actual = Run(@"
            .function public uint8[3][6] foo
                ldsfld bar
                ret
            .global public uint8[3][6] bar");
        return Verify(actual);
    }

    [Test]
    public Task ValueArray6()
    {
        var actual = Run(@"
            .function public uint8[3]*[6] foo
                ldsfld bar
                ret
            .global public uint8[3]*[6] bar");
        return Verify(actual);
    }

    [Test]
    public Task ValueArrayWithBoolean()
    {
        var actual = Run(@"
            .function public bool[3]*[6] foo
                ldsfld bar
                ret
            .global public bool[3]*[6] bar");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task Structure1()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task Structure2()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo 2
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task Structure3()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo explicit
                public int32 a 0
                public int8 b 2
                public int32 c 6");
        return Verify(actual);
    }

    [Test]
    public Task Structure4()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                public bar b
                public int32 c
            .structure public bar
                public int16 a
                public int64 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task Structure5()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int16 a
                public int64 b
                public int32 c
            .structure public foo
                public int16 a
                public int64 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task Structure6()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo 2
                public int16 a
                public int64 b
                public int32 c
            .structure public foo 2
                public int16 a
                public int64 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task Structure7()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo explicit
                public int16 a 0
                public int64 b 2
                public int32 c 4
            .structure public foo explicit
                public int16 a 0
                public int64 b 2
                public int32 c 4");
        return Verify(actual);
    }

    [Test]
    public Task Structure8()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                public bar b
                public int32 c
            .structure public bar
                public int16 a
                public int64 b
                public int32 c
            .structure public foo
                public int32 a
                public bar b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task Structure9()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                internal int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task StructureWithFlexibleArray()
    {
        var actual = Run(@"
            .function public void main
                .local foo* pfoo
                ldc.i4.0
                stloc pfoo
                ret
            .structure public foo
                public int32 a
                public int8 b
                public int32[*] c");
        return Verify(actual);
    }

    [Test]
    public Task StructureWithBoolean()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public bool a");
        return Verify(actual);
    }

    [Test]
    public Task StructureWithArray1()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                public int8[4] b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task StructureWithArray2()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                public int8[4][3] b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task StructureWithArray3()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure public foo
                public int32 a
                public bar[3] b
                public int32 c
            .structure public bar
                public int32 a
                public int8[4] b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeStructure()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure internal foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeStructure()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure file foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeStructureReference1()
    {
        var actual = Run(@"
            .function internal int32 main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure internal foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeStructureReference2()
    {
        var actual = Run(@"
            .function internal int32 main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure internal foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeStructureReference1()
    {
        var actual = Run(@"
            .function file int32 main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure internal foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeStructureReference2()
    {
        var actual = Run(@"
            .function file int32 main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .structure file foo
                public int32 a
                public int8 b
                public int32 c");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task FunctionWithBoolean1()
    {
        var actual = Run(@"
            .function public bool foo
                ldc.i4.1
                ret");
        return Verify(actual);
    }

    [Test]
    public Task FunctionWithBoolean2()
    {
        var actual = Run(@"
            .function public void foo a:bool
                ret");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////
    [Test]
    public Task Enumeration1()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public int32 foo
                beef
                poke
                chicken");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration2()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public int32 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration3()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public int8 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration4()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public uint8 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration5()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public int16 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration6()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public uint16 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration7()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public int32 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration8()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public uint32 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration9()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public int64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task Enumeration10()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration public uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeEnumeration()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration internal uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeEnumeration()
    {
        var actual = Run(@"
            .function public void main
                .local foo fv
                ldloca 0
                initobj foo
                ret
            .enumeration file uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeEnumerationReference1()
    {
        var actual = Run(@"
            .function internal int32 main
                .local foo fv
                ldloc 0
                ret
            .enumeration internal uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task InternalScopeEnumerationReference2()
    {
        var actual = Run(@"
            .function internal int32 main
                .local foo fv
                ldloc 0
                ret
            .enumeration file uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeEnumerationReference1()
    {
        var actual = Run(@"
            .function file int32 main
                .local foo fv
                ldloc 0
                ret
            .enumeration internal uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    [Test]
    public Task FileScopeEnumerationReference2()
    {
        var actual = Run(@"
            .function file int32 main
                .local foo fv
                ldloc 0
                ret
            .enumeration file uint64 foo
                beef 5
                poke 13
                chicken 42");
        return Verify(actual);
    }

    /////////////////////////////////////////////////////////

    [Test]
    public Task TfmSpecific10()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net10");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific11()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net11");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific20()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net20");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific30()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net30");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific35()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net35");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific35Client()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net35-client");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific40()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net40");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific40Client()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net40-client");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific45()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net45");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecific48()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net48");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificStandard10()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netstandard1.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificStandard16()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netstandard1.6");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificStandard20()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netstandard2.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificStandard21()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netstandard2.1");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp10()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp1.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp11()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp1.1");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp20()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp2.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp21()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp2.1");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp22()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp2.2");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp30()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp3.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp31()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "netcoreapp3.1");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp50()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net5.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp60()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net6.0");
        return Verify(actual);
    }

    [Test]
    public Task TfmSpecificCoreApp70()
    {
        var actual = Run(@"
            .function public int32 main
                ldc.i4.1
                ret",
                targetFrameworkMoniker: "net7.0");
        return Verify(actual);
    }
}
