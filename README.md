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
| chibias.build (MSBuild scripting) | [![NuGet chibias.build](https://img.shields.io/nuget/v/chibias.build.svg?style=flat)](https://www.nuget.org/packages/chibias.build) |


----

## How to use

Install CLI via nuget package [chibias-cli](https://www.nuget.org/packages/chibias-cli). (NOT 'chibias-cil' :)

```bash
$ dotnet tool install -g chibias-cli
```

Then:

```bash
$ chibias

chibias [0.15.0,net6.0]
This is the CIL assembler, part of chibicc-cil project.
https://github.com/kekyo/chibias-cil
Copyright (c) Kouji Matsui
License under MIT

usage: chibias [options] <source path> [<source path> ...]
  -o <path>         Output assembly path
  -c, --dll         Produce dll assembly
      --exe         Produce executable assembly (defaulted)
      --winexe      Produce Windows executable assembly
  -r                Reference assembly path
  -g, -g2           Produce embedded debug symbol (defaulted)
      -g1           Produce portable debug symbol file
      -gm           Produce mono debug symbol file
      -gw           Produce windows proprietary debug symbol file
      -g0           Omit debug symbol file
  -O, -O1           Apply optimization
      -O0           Disable optimization (defaulted)
  -v <version>      Apply assembly version (defaulted: 1.0.0.0)
  -f <tfm>          Target framework moniker (defaulted: net6.0)
      --log <level> Log level [debug|trace|information|warning|error|silent]
  -h, --help        Show this help
```

* chibias will combine multiple source code in command line pointed into one assembly.
* Reference assembly paths evaluates last-to-first order, same as `ld` looking up.
  This feature applies to duplicated symbols (function/global variables).
  
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
$ chibias -r /mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll -o hello.exe hello.s
```

Run it:

```bash
$ ./hello.exe
Hello world with chibias!
```

Yes, this example uses the `System.Console.WriteLine()` defined in the `mscorlib` assembly file in the
Windows environment (WSL). But now you know how to reference assemblies from chibias.

Linux and other operating systems can be used in the same way, by adding references you need.
Or, if you have assembled code that is purely computational, you do not need any references to other assemblies:

```
.function int32 main
    ldc.i4.1
    ldc.i4.2
    add
    ret
```

```bash
$ chibias -o adder.exe adder.s
$ ./adder.exe
$ echo $?
3
```

### FYI: How to get mscorlib or others?

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

Simply specify a method with full name and parameter types:

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

### Global initializer

The Initializer directive is the same as the Function directive except that there is no return type, function name, or parameters.
This is used to write custom code to initialize global variables:

```
.initializer
    ldc.i4 123
    stsfld foo
    ret
.global int32 foo
```

Initializer directives may be used any number of times in the source code.
They are called from the real type initializer of the `C.data` class.

However, the order cannot be specified.
The relationship of one Initializer depending on the other is not taken into account.

### Constant data

The Constant directive places fixed data in the assembly that will not change:

```
.function uint8[] foo
    ldc.i4.6
    newarr uint8
    dup
    ldtoken bar
    call System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray System.Array System.RuntimeFieldHandle
    ret
.constant bar 0x01 0x02 0x31 0x32 0xb1 0xb2
```

Placed fixed data is placed directly into a dedicated hidden structure type and can be referenced by `ldtoken` opcode.
The standard scenario is to have an array of initial values as shown above.

Elements placed in a constant directive are similar to global variables, but differ in several ways:

* Because it is marked as private, it can only be accessed by CABI members in the same assembly.
  It cannot be referenced from outside the assembly.
* Symbol names (`bar` in the above example) are treated like global variables and can be referenced by `ldsfld` opcode and likes.
  However, since the type of the retrieved instance will be of its own value type, and should be handled with care.

### Structure

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

When you use explicitly array length type like `int8[3]`,
chibias will generate custom type automatically include each item fields.

```
.structure foo
    int32 a
    int8[3] b    ; <-- Array type for explicitly length
    int32 c
```

Pseudo code in C#:

```csharp
namespace C.type;

[StructLayout(LayoutKind.Sequential)]
public struct foo
{
    public int a;
    public int8_len3 b;    // <-- int8[3]
    public int c;
}

[StructLayout(LayoutKind.Sequential)]
public struct int8_len3    // : IList<sbyte>  // TODO:
{
    public sbyte Item0;
    public sbyte Item1;
    public sbyte Item2;
}
```

### Explicitly location information

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

The file and location directive will produce sequence points into debugging information.

* The file directive maps ID to source code file.
  * First operand: ID (Valid any symbols include number, same as GAS's `.file` directive).
  * Second operand: File path (or source code identity) string.
  * Third operand: Language indicator, see listing below. (Optional)
  * The file directive can always declare, and will overwrite same ID.
* The location directive indicates source code location.
  * First operand: ID for referring source code file.
  * Second operand: Start line index. (0 based index)
  * Third operand: Start column index. (0 based index)
  * Forth operand: End line index. (0 based index)
  * Fifth operand: End column index. (0 based index, must larger than start)
  * The location directive can declare only in the function/initializer body.

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
* Automatic implements `IList<T>` on array type for explicitly length.
* Handling variable arguments.
* Handling method optional attributes (inline, no-inline and no-optimizing?)
* Handling for target framework moniker.
  * Refers `System.Object` from `C.text` base class, is it referenced to `mscorlib` or `System.Runtime` ?
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
