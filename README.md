# The specialized backend CIL assembler for chibicc-cil

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)

## NuGet

| Package  | NuGet                                                                                                                |
|:---------|:---------------------------------------------------------------------------------------------------------------------|
| chibias-cli (dotnet CLI) | [![NuGet chibias-cli](https://img.shields.io/nuget/v/chibias-cli.svg?style=flat)](https://www.nuget.org/packages/chibias-cli) |
| chibias.core (Core library) | [![NuGet chibias.core](https://img.shields.io/nuget/v/chibias.core.svg?style=flat)](https://www.nuget.org/packages/chibias.core) |
| chibias.build (MSBuild scripting) | [![NuGet chibias.build](https://img.shields.io/nuget/v/chibias.build.svg?style=flat)](https://www.nuget.org/packages/chibias.build) |

## What is this?

[![Japanese language](Images/Japanese.256.png)](https://github.com/kekyo/chibias-cil/blob/main/README.ja.md)

This is a CIL/MSIL assembler, backend for porting C language compiler implementation derived from [chibicc](https://github.com/rui314/chibicc) on .NET CIL/CLR.
It is WIP and broadcasting side-by-side Git commit portion on [YouTube (In Japanese)](https://bit.ly/3XbqPSQ).

[chibicc-cil](https://github.com/kekyo/chibicc-cil) will be made available as the porting progresses to some extent, please wait.


----

## Overview

chibias takes multiple CIL source codes as input, performs assembly, and outputs the result as . NET assemblies. At this time, reference assemblies can be specified so that they can be referenced from the CIL source code.

![chibias overview](Images/chibias.png)

chibias was developed as a backend assembler for chibicc, but can also be used by us.
The source code is easier to write for humans because it employs simplified syntax rules compared to ILAsm.

The general C compiler generates intermediate object format files `*.o` by inputting them to the linker `ld` at the final stage. chibias does not handle such object files, but generates `exe` and `dll` directly. When dealing with split source code, you can consider the source code itself (`*.s`) as an intermediate object format file and treat it same way as a linker.


----

## How to use

Install CLI via nuget package [chibias-cli](https://www.nuget.org/packages/chibias-cli). (NOT 'chibias-cil' :)

```bash
$ dotnet tool install -g chibias-cli
```

Then:

```bash
$ chibias

chibias [0.18.0,net6.0] [...]
This is the CIL assembler, part of chibicc-cil project.
https://github.com/kekyo/chibias-cil
Copyright (c) Kouji Matsui
License under MIT

usage: chibias [options] <source path> [<source path> ...]
  -o <path>         Output assembly path
  -c, --dll         Produce dll assembly
      --exe         Produce executable assembly (defaulted)
      --winexe      Produce Windows executable assembly
  -r <path>         Reference assembly path
  -g, -g2           Produce embedded debug symbol (defaulted)
      -g1           Produce portable debug symbol file
      -gm           Produce mono debug symbol file
      -gw           Produce windows proprietary debug symbol file
      -g0           Omit debug symbol file
  -O, -O1           Apply optimization
      -O0           Disable optimization (defaulted)
  -s                Suppress runtime configuration file
  -v <version>      Apply assembly version (defaulted: 1.0.0.0)
  -f <tfm>          Target framework moniker (defaulted: net6.0)
      --log <level> Log level [debug|trace|information|warning|error|silent]
  -h, --help        Show this help
```

* chibias will combine multiple source code in command line pointed into one assembly.
* Reference assembly paths evaluates last-to-first order, same as `ld` looking up.
  This feature applies to duplicated symbols (function/global variables).
* The default target framework moniker (`net6.0` in the above example) depends on the operating environment of chibias.
* Specifying a target framework moniker only assumes a variation of the core library.
  And it does NOT automatically detect the `mscorlib.dll` or `System.Private.CoreLib.dll` assembly files (see below).


----

## Hello world

Let's play "Hello world" with chibias.
You should create a new source code file `hello.s` with the contents only need 4 lines:

```
.function void main
    ldstr "Hello world with chibias!"
    call System.Console.WriteLine string
    ret
```

Then invoke chibias with:

```bash
$ chibias -f net45 -r /mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll -o hello.exe hello.s
```

Run it:

```bash
$ ./hello.exe
Hello world with chibias!
```

Yes, this example uses the `System.Console.WriteLine()` defined in the `mscorlib.dll` assembly file in the
Windows environment (WSL). But now you know how to reference assemblies from chibias.

Linux and other operating systems can be used in the same way, by adding references you need.
Also, if you assemble code that uses only built-in types (see below), you do not need references to other assemblies:

```
.function int32 main
    ldc.i4.1
    ldc.i4.2
    add
    ret
```

```bash
$ chibias -f net45 -o adder.exe adder.s
$ ./adder.exe
$ echo $?
3
```

### To run with .NET 6, .NET Core and others

Specify the target framework moniker and make sure that the reference assembly `System.Private.CoreLib.dll`:

```bash
$ chibias -f net6.0 -r ~/.dotnet/shared/Microsoft.NETCore.App/6.0.13/System.Private.CoreLib.dll -o hello.exe hello.s
```

The version of the target framework moniker and the corresponding core library must match.
If you specify `net6.0` and use the .NET Core 2.2 core library, you will get a warning.

Note: Minor target framework monikers are not currently supported.
For example, `uap10.0`, `tizen1` and `portable+net45+wp5+sl5`.
This is because the standard target framework moniker parser is not included in the BCL.

### FYI: How do I get the core assembly files?

If you want to obtain `mscorlib.dll` legally,
you can use the [ReferenceAssemblies (net45)](https://www.nuget.org/packages/microsoft.netframework.referenceassemblies.net45) package.

This package is provided by MS under the MIT license, so you are free to use it.
The `nupkg` file is in zip format, so you can use `unzip` to extract the contents.

It is important to note that all of the assemblies included in this package do not have any code bodies.
It is possible to reference them with chibias, but it is not possible to run them.
If you want to run it in a Linux environment or others, you will need a runtime such as mono/.NET Core.

```bash
$ mono ./hello.exe
Hello world with chibias!
```

In any case, if you want to refer to the complete `mscorlib.dll` or `System.Private.CoreLib.dll` files,
it may be better to simply install mono and/or .NET SDK and reference the files in that directory.

At the moment, chibias does not automatically detect these assembly files installed on the system.
This is by design as stand-alone independent assembler, like the GNU assembler.
In the future, it may be possible to resolve assembly files automatically via the MSBuild script.

(The `chibias.build` package is available for this purpose. But it is still incomplete and cannot be used now.)


----

## Assembly syntax

TODO: WIP, Specifications have not yet been finalized.

To check the syntax, you should look at [the test code](https://github.com/kekyo/chibias-cil/blob/main/chibias.core.Tests/AssemblerTests.cs).

The syntax of chibias has the following features:

* The body of the opcode can be written in almost the same way as in ILAsm.
* Unnecessarily verbose descriptions are eliminated as much as possible.
* Codes related to OOP cannot be written or have restrictions

It is basically designed to achieve a "C language" like chibicc,
but it should be much easier to write than ILAsm.

### Minimum, included only main entry point

```
.function int32 main
    ldc.i4 123    ; This is comment.
    ret
```

* Source code decoding with UTF-8.
* The line both pre-whitespaces and post-whitespaces are ignored.
  * That is, indentation is simply ignored.
* The semicolon (';') starts comment, ignores all words at end of line.
* Begin a word with dot ('.') declaration is "Assembler directives."
  * `.function` directive is beginning function body with return type and function name.
  * The function body continues until the next function directive appears.
* Automatic apply entry point when using `main` function name and assemble executable file with same as `--exe` option.

### Literals

```
.function int32 main
    ldc.i4 123
    ldc.r8 1.234
    ldstr "abc\"def\"ghi"
    pop
    pop
    ret
```

* Numeric literal formats are compatible with .NET Format provider.
  * Integer literal: `System.Int32.Parse()` and suitable types with `InvariantCulture`.
  * Floating point number literal: `System.Double.Parse()` and suitable types with `InvariantCulture`.
* String literal is double-quoted ('"').
  * Escape character is ('\\'), same as C language specification except trigraph chars.
  * Hex number ('\\xnn') and UTF-16 ('\\unnnn') numbers are acceptable.

### Labels

```
.function int32 main
    ldc.i4 123
    br NAME
    nop
NAME:
    ret
```

Label name ends with (':').
Label name requires unique in the function scope.

### Builtin type names

|Type name|Exact type|Alias type names|
|:----|:----|:----|
|`void`|`System.Void`| |
|`uint8`|`System.Byte`|`byte`|
|`int8`|`System.SByte`|`sbyte`|
|`int16`|`System.Int16`|`short`|
|`uint16`|`System.UInt16`|`ushort`|
|`int32`|`System.Int32`|`int`|
|`uint32`|`System.UInt32`|`uint`|
|`int64`|`System.Int64`|`long`|
|`uint64`|`System.UInt64`|`ulong`|
|`float32`|`System.Single`|`float`, `single`|
|`float64`|`System.Double`|`double`|
|`intptr`|`System.IntPtr`|`nint`|
|`uintptr`|`System.UIntPtr`|`nuint`|
|`bool`|`System.Boolean`| |
|`char`|`System.Char`|`char16`|
|`object`|`System.Object`| |
|`string`|`System.String`| |
|`typeref`|`System.TypedReference`| |

You can combine array/pointer/refernces.
(Separated with white space is not allowed.)

* `int[]`
* `int[][]`
* `int[4]`
* `int[4][3]`
* `int32*`
* `int32**`
* `int32&`

A type that specifies the number of elements in an array is called a "Value array type" and is treated differently from an array in .NET CLR.
See separate section for details.

### Local variables

```
.function int32 main
    .local int32
    .local int32 abc
    ldc.i4 1
    stloc 0
    ldc.i4 2
    stloc 1
    ret
```

We can declare local variables with `.local` directive inside function body.
The local directive could have optional variable name.

We can refer with variable name in operand:

```
.function void foo
    .local int32 abc
    ldc.i4 1
    stloc abc
    ret
```

### Call another function

```
.function int32 main
    ldc.i4 1
    ldc.i4 2
    call add2
    ret
.function int32 add2 x:int32 y:int32
    ldarg 0
    ldarg y   ; We can refer by parameter name
    add
    ret
```

The parameters are optional. Formats are:

* `int32`: Only type name.
* `x:int32`: Type name with parameter name.

The function name both forward and backaward references are accepted.

Important: If you are calling a function defined in chibias,
you do not need to specify any argument type list for the `call` operand.
In another hand, .NET overloaded methods, an argument type list is required.

### Call external function

Before assemble to make `test.dll`

```
.function int32 add2 a:int32 b:int32
    ldarg 0
    ldarg 1
    add
    ret
```

```bash
$ chibias -c test.s
```

Then:

```
.function int32 main
    ldc.i4 1
    ldc.i4 2
    call add2
    ret
```

```bash
$ chibias -r test.dll main.s
```

The functions (.NET CIL methods) are placed into single class named `C.text`.
That mapping is:

* `int32 main` --> `public static int32 C.text::main()`
* `int32 add2 a:int32 b:int32` --> `public static int32 C.text::add2(int32 a, int32 b)`

Pseudo code in C# (test.dll):

```csharp
namespace C;

public static class text
{
    public static int add2(int a, int b) => a + b;
}
```

Pseudo code in C# (main.exe):

```csharp
extern alias test;
using test_text = test::C.text;

namespace C;

public static class text
{
    public static int main() => test_text::add2(1, 2);
}
```

This is named "CABI (chibicc application binary interface) specification."

### Call external method

Simply specify a .NET method with full name and parameter types:

```
.function void main
    ldstr "Hello world"
    call System.Console.WriteLine string
    ret
```

The method you specify must be public, and could not refer method with any generic parameters.
Instance methods can also be specified, but of course `this` reference must be pushed onto the evaluation stack.

A list of parameter types is used to identify overloads.

You have to give it containing assembly on command line option `-r`.
This is true even for the most standard `mscorlib.dll` or `System.Runtime.dll`.

### Call site syntax

The call site is the signature descriptor of the target method that must be indicated by the `calli` opcode:

```
.function int32 main
    ldstr "123"
    ldftn System.Int32.Parse string
    calli int32 string
    ret
```

The call site specifies the return type and a list of parameter types.

### Global variables

Global variable directive format is same as local variable directive,
However, excludes declarations outside function body:

```
.function int32 main
    ldc.i4 123
    stsfld foo
    ldsfld foo
    ret
.global int32 foo
```

The global variable name both forward and backaward references are accepted.

Global variable name strategy complies with CABI excepts placing into `C.data` class.

Pseudo code in C#:

```csharp
namespace C;

public static class data
{
    public static int foo;
}

public static class text
{
    public static int main()
    {
        data.foo = 123;
        return data.foo;
    }
}
```

### Initializing data

The global variable declares with initializing data:

```
.function int32 bar
    ldsfld foo
    ret
; int32 foo = 0x76543210
.global int32 foo 0x10 0x32 0x54 0x76
```

The data must be fill in bytes.
In addition, since the placed data will be writable,
care must be taken in handling it.

### Value array type

.NET does not have an array type that behaves like a value type.
chibias can use the `value array` type to pseudo-realize this.
The value array type plays a very important role in the realization of the C language compiler.

To use a value array type, declare the type as follows:

```
.function int8[5] bar   ; <-- Value array requres element length
    ldsfld foo
    ret
.global int8[5] foo 0x10 0x32 0x54 0x76 0x98
```

At this time, the actual type of the `bar` function and the `foo` variable will be of type `System.SByte_len5`.
Specifically, the following structure is declared automatically.

Pseudo code in C#:

```csharp
namespace System;

[StructLayout(LayoutKind.Sequential)]
public struct SByte_len5   // TODO: : IList<sbyte>, IReadOnlyList<sbyte>
{
    private sbyte item0;
    private sbyte item1;
    private sbyte item2;
    private sbyte item3;
    private sbyte item4;

    public int Length => 5;
    public sbyte this[int index]
    {
        get => /* ... */;
        set => /* ... */;
    }
}
```

This structure can behave likes an array outside of chibias (and chibicc).

The natural interpretation of types is also performed.
For example:

* `int8[5]*` --> `System.SByte_len5*`
* `int8*[5]` --> `System.SByte_ptr_len5`
* `int8[5][3]` --> `System.SByte_len5_len3`
* `int8[5]*[3]` --> `System.SByte_len5_ptr_len3`

Note: To use the value array type, references to the types `System.ValueType` and `System.IndexOutOfRangeException` must be resolved.
Add a reference to `mscorlib.dll` or `System.Private.CoreLib.dll`.

### Structure type

The only types that can be defined are structure types.
That is, types that inherit implicitly from `System.ValueType`:

```
.structure foo
    int32 a
    int8 b
    int32 c
```

Pseudo code in C#:

```csharp
namespace C.type;

[StructLayout(LayoutKind.Sequential)]
public struct foo
{
    public int a;
    public sbyte b;
    public int c;
}
```

By default, structure packing is left to the CLR.
To specify explicitly:

```
.structure foo 4  ; pack=4
    int32 a
    int8 b
    int32 c
```

Or gives an offset to each member:

```
.structure foo explicit
    int32 a 0     ; offset=0
    int8 b 4      ; offset=4
    int32 c 5     ; offset=5
```

By arbitrarily adjusting the offset, we can reproduce the union type in the C language.

Note: To use the value array type, references to the type `System.ValueType` must be resolved.
Add a reference to `mscorlib.dll` or `System.Private.CoreLib.dll`.

### Explicitly location information

The file and location directive will produce sequence points into debugging information.
Sequence points are used to locate where the code being executed corresponds to in the source code.
In other words, when this information is given,
the debugger will be able to indicate the location of the code being executed in the source code.

This information is optional and does not affect assembly task if it is not present.

```
.file 1 "/home/kouji/Projects/test.c" c
.function int32 main
    .location 1 10 5 10 36
    ldc.i4 123
    ldc.i4 456
    add
    .location 1 11 5 11 32
    ldc.i4 789
    sub
    ret
```

* The file directive maps ID to source code file.
  * First operand: ID (Valid any symbols include number, same as GNU assembler's `.fil` directive).
  * Second operand: File path (or source code identity) string.
  * Third operand: Language indicator, see listing below. (Optional)
  * The file directive can always declare, and will overwrite same ID.
* The location directive indicates source code location.
  * First operand: ID for referring source code file.
  * Second operand: Start line index. (0 based index)
  * Third operand: Start column index. (0 based index)
  * Forth operand: End line index. (0 based index)
  * Fifth operand: End column index. (0 based index, must larger than start)
  * The location directive can declare only in the function body.

The language indicators is shown (not all):

|Language indicator|Language|
|:----|:----|
|`cil`|CIL|
|`c`|C|
|`cpp`|C++|
|`csharp`|C#|
|`fsharp`|F#|
|`other`|-|

Language indicator comes from [Mono.Cecil.Cil.DocumentLanguage](https://github.com/jbevain/cecil/blob/7b8ee049a151204997eecf587c69acc2f67c8405/Mono.Cecil.Cil/Document.cs#L27).

Will produce debugging information with CIL source file itself when does not apply any location directive.


----

## TODO

Might be implemented:

* `OperandType`
  * InlineSwitch
* Handling function/global variable scopes.
* Automatic implements `IList<T>` on value array type.
* Handling variable arguments.
* Handling method optional attributes (inline, no-inline and no-optimizing?)
* Generate CIL `Main(args)` handler and bypass to C specific `main(argc, argv)` function.
* And chibicc-cil specific requirements...

Might not be implemented:

* `OperandType`
  * InlinePhi
* Handling multi-dimensional array types.
* Exception handling.
* And NOT chibicc-cil specific requirements.


----

## Background

Initially I used [ILAsm.Managed](https://github.com/kekyo/ILAsm.Managed), but there were some issues:

* Resolving assembly references:
  ILAsm requires explicitly qualifying the assembly name when referring to a member defined in an external assembly,
  but this is not easy to resolve in internal compiler.
* Calculation of `.maxstack`:
  To measure the overall consumption of the evaluation stack, the execution flow must be analyzed in internal compiler.
* Debugging information:
  ILAsm can provide debugging information, but it also has the following problems:
  * Debugging information from CIL source code itself, but it is not possible to create references to the original source code.
    This corresponds to `.fil` and `.loc` directives in GNU assembler.
    Also, there was a possibility to provide richer debugging information in .NET assembly,
    but it is not possible as far as using ILAsm.
  * Managed debugging information is in "MDB" format, which is not suitable for the current situation.
    So it is necessary to be able to realize "Portable PDB" or "Embedded."
    This could be solved by using the ILAsm provided by MS, but we have decided not to use it because of [another problem](https://github.com/kekyo/ILAsm.Managed#background).
* ILAsm has complex and redundant syntax rules.
  * Want to eliminate excessive curly-brace syntax. ('{ ... }')
  * Want to eliminate unusual optional member attributes.
  * Want to eliminate excessive namespace/type member prefixes.

These were considered difficult to work around and led us to implement our own assembler with the minimum required functionality.

I am implementing this in parallel with the porting of chibicc-cil, adding and changing features that we have newly discovered. For example:

* Added value array type feature.
* Combined initializer into global variables.
* Definition of CABI

----

## License

Under MIT.
