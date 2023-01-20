# The specialized backend CIL assembler for chibicc-cil

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)

This is a CIL/MSIL assembler, backend for porting C language compiler implementation derived from [chibicc](https://github.com/rui314/chibicc) on .NET CIL/CLR.
It is WIP and broadcasting side-by-side GIT commit portion on [YouTube (In Japanese)](https://bit.ly/3XbqPSQ).

[chibicc-cil](https://github.com/kekyo/chibicc-cil) will be made available as the porting progresses to some extent, please wait.


### NuGet

| Package  | NuGet                                                                                                                |
|:---------|:---------------------------------------------------------------------------------------------------------------------|
| chibias-cli (dotnet CLI) | [![NuGet chibias-cli](https://img.shields.io/nuget/v/chibias-cli.svg?style=flat)](https://www.nuget.org/packages/chibias-cli) |
| chibias.core (Core library) | [![NuGet chibias.core](https://img.shields.io/nuget/v/chibias.core.svg?style=flat)](https://www.nuget.org/packages/chibias.core) |


----

## How to use

Install CLI via nuget package [chibias-cli](https://www.nuget.org/packages/chibias-cli). (NOT 'chibias-cil' :)

```bash
$ dotnet tool install -g chibias-cli
```

Then:

```bash
$ chibias

chibias [0.0.4,net6.0]
This is the CIL assembler, part of chibicc-cil project.
https://github.com/kekyo/chibias-cil
Copyright (c) Kouji Matsui
License under MIT

usage: chibias [options] <source path> [<source path> ...]
  -o=VALUE                   Output assembly path
  -c, --dll                  Produce dll assembly
      --exe                  Produce executable assembly (defaulted)
      --winexe               Produce Windows executable assembly
  -r, --reference=VALUE      Reference assembly path
  -g, --g1, --portable       Produce portable debug symbol file (defaulted)
      --g0, --no-debug       Omit debug symbol file
      --g2, --embedded       Produce embedded debug symbol
  -O                         Apply optimization
      --O0                   Disable optimization (defaulted)
      --asm-version=VALUE    Apply assembly version
      --tfm=VALUE            Target framework moniker (defaulted: net6.0)
      --log=VALUE            Log level [debug|trace|information|warning|error|silent]
  -h, --help                 Show this help
```

* chibias will combine multiple source code in command line pointed into one assembly.
* Reference assembly paths evaluates last-to-first order, same as `ld` looking up.
  This feature applies to duplicated symbols (function/global variables).

----

## Assembly syntax

TODO: WIP, Specifications have not yet been finalized.
To check the syntax, you should look at [the test code](https://github.com/kekyo/chibias-cil/blob/main/chibias.core.Tests/AssemblerTests.cs#L135).

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
* Begin with dot ('.') declaration is "Assembler directives."
  * `.function` directive is beginning function body with return type and function name.
  * The function body continues until the next function directive appears.
* Automatic apply entry point when using `main` function name and assemble with `--exe` option.

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
  * Escape character is ('\'), same as C language specification except trigraph chars.
  * Hex number ('\xnn') and UTF-16 ('\unnnn') numbers are acceptable.

### Labels

```
.function int32 main
    ldc.i4 123
    br NAME
    nop
NAME:
    ret
```

Label name ends with ':'.
Label name requires unique in the function scope.

### Builtin types

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
* `int32*`
* `int32**`
* `int32&`

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

### Call another function

```
.function int32 main
    ldc.i4 1
    ldc.i4 2
    call add2
    ret
.function int32 add2 a:int32 b:int32
    ldarg 0
    ldarg 1
    add
    ret
```

The parameters are optional. Formats are:

* `int32`: Only type name.
* `a:int32`: Type name with parameter name.

The function name both forward and backaward references are accepted.

Important topic: The `call` operand is NOT containing any argument description.
In other words, chibias cannot call .NET overloaded methods directly.

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

The functions (.NET CIL methods) are placed into single class named `C.module`.
That mapping is:

* `int32 main` --> `public static int32 C.module::main()`
* `int32 add2 a:int32 b:int32` --> `public static int32 C.module::add2(int32 a, int32 b)`

Pseudo code in C# (test.dll):

```csharp
namespace C;

public static class module
{
    public static int add2(int a, int b) => a + b;
}
```

Pseudo code in C# (main.exe):

```csharp
extern alias test;
using test_module = test::C.module;

namespace C;

public static class module
{
    public static int main() => test_module::add2(1, 2);
}
```

This is named "CABI (chibicc application binary interface) specification."

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

Global variable name strategy complies with CABI.

Pseudo code in C#:

```csharp
namespace C;

public static class module
{
    public static int main()
    {
        foo = 123;
        return foo;
    }
    public static int foo;
}
```

### Override maximum evaluation stack size

```
.function int32 main
    .maxstack 100
    ldc.i4 123
    ret
```

chibias will try calculation for using maximum evaluation stack by static execution flow analysis.
It is not perfectly working, so it will override when this directive is declared.

### Location information

```
.function int32 main
    .location 10 "/home/kouji/Projects/test.c" c
    ldc.i4 123
    ldc.i4 456
    add
    .location 11
    ldc.i4 789
    sub
    ret
```

The location directive will produce sequence points into debugging information.

* First operand: Line number (1 based index).
* Second and third operands: Source file path and language indicator (Optional). Ignored case sensitive.

|Language indicator|Language|
|:----|:----|
|`cil`|CIL|
|`c`|C|
|`cpp`|C++|
|`csharp`|C#|
|`fsharp`|F#|

Language indicator comes from [Mono.Cecil.Cil.DocumentLanguage](https://github.com/jbevain/cecil/blob/7b8ee049a151204997eecf587c69acc2f67c8405/Mono.Cecil.Cil/Document.cs#L27).

Will produce debugging information with CIL source file itself when does not apply any location directive.


----

## TODO

Might be implemented:

* `OperandType`
  * InlineSwitch
  * InlineTok (?)
* Handling variable arguments.
* Handling value type declaration.
* Handling method optional attributes (inline, no-inline and no-optimizing?)
* Handling for target framework moniker.
  * Refers `System.Object` from `C.module` base class, is it referenced to `mscorlib` or `System.Runtime` ?
* Better handling for line-based number information.
* Generate CIL `Main(args)` handler and bypass to C specific `main(argc, argv)` function.
* Custom command line parser.
* MSBuild scriptable packaging.
* And chibicc-cil specific requirements...

Might not be implemented:

* `OperandType`
  * InlinePhi,
  * InlineSig
* Handling multi-dimensional array types.
* Handling parameter/variable names in operand.
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
    This corresponds to `.file` and `.loc` directives in GNU assembler.
    Also, there was a possibility to provide richer debugging information in .NET assembly,
    but it is not possible as far as using ILAsm.
  * Managed debugging information is in "MDB" format, which is not suitable for the current situation.
    So it is necessary to be able to realize "Portable PDB" or "Embedded."
    * This could be solved by using the ILAsm provided by MS, but we have decided not to use it because of [another problem](https://github.com/kekyo/ILAsm.Managed#background).
* ILAsm has complex and redundant syntax rules.
  * Want to eliminate excessive block-quote syntax.
  * Want to eliminate unusual optional member attributes.
  * Want to eliminate chatty namespace/type member prefixes.

These were considered difficult to work around and led us to implement our own assembler with the minimum required functionality.


----

## License

Under MIT.
