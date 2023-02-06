# The specialized backend CIL assembler for chibicc-cil

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)

## NuGet

| Package  | NuGet                                                                                                                |
|:---------|:---------------------------------------------------------------------------------------------------------------------|
| chibias-cli (dotnet CLI) | [![NuGet chibias-cli](https://img.shields.io/nuget/v/chibias-cli.svg?style=flat)](https://www.nuget.org/packages/chibias-cli) |
| chibias.core (Core library) | [![NuGet chibias.core](https://img.shields.io/nuget/v/chibias.core.svg?style=flat)](https://www.nuget.org/packages/chibias.core) |
| chibias.build (MSBuild scripting) | [![NuGet chibias.build](https://img.shields.io/nuget/v/chibias.build.svg?style=flat)](https://www.nuget.org/packages/chibias.build) |

## これは何?

[English language is here](https://github.com/kekyo/chibias-cil)

これは、.NET CIL/CLR 上に [chibicc](https://github.com/rui314/chibicc) を移植するためのバックエンドとなるCIL/MSILアセンブラです。
まだ作業中ですが、[YouTube](https://bit.ly/3XbqPSQ) にて、Gitコミット毎に移植する解説シリーズを放送しています。

[chibicc-cil](https://github.com/kekyo/chibicc-cil) は、ある程度移植が進んだ段階で公開する予定です。しばらくお待ちください。


----

## chibiasの全体像

chibiasは、複数のCILソースコードを入力として、アセンブルを行い、結果を.NETアセンブリとして出力します。この時、参照アセンブリ群を指定して、ソースコードから参照出来るようにします。

![chibias overview](Images/chibias.png)

chibiasはchibiccのバックエンドアセンブラとして開発しましたが、単独でも使用可能です。
ソースコードは、ILAsmと比べて簡素化された構文規則を採用するため、人間にとっても書きやすくなっています。

一般的なCコンパイラは、最終段階でリンカ `ld` に中間形式ファイル `*.o` を入力して生成しますが、chibiasはこのようなファイルを扱わずに、直接 `exe` や `dll`を生成します。chibiasは、リンカの機能を兼ねていると考えることが出来て、分割されたソースコードを扱う場合は、ソースコード自体 (`*.s`) を中間形式ファイルとみなせば、リンカと同様に扱うことが出来ます。


----

## 使用方法

CLIバージョンのchibiasを、nuget [chibias-cli](https://www.nuget.org/packages/chibias-cli) からインストール出来ます (紛らわしいのですが、 'chibias-cil' ではありません :)

```bash
$ dotnet tool install -g chibias-cli
```

使用可能になったかどうかは、以下のように確認できます:

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

* chibiasは、コマンドラインで指摘された複数のソースコードをアセンブルして、1つの.NETアセンブリにまとめます。
* 参照アセンブリパスは、`ld` のライブラリルックアップと同じように最後から順に評価されます。
  この機能は、重複するシンボル(関数/グローバル変数)にも適用されます。

----

## Hello world

chibiasを使って "Hello world" を実行してみましょう。
新しいソースコード・ファイル `hello.s` を作り、以下のようにコードを書きます。この4行だけでOKです:

```
.function void main
    ldstr "Hello world with chibias!"
    call System.Console.WriteLine string
    ret
```

出来たら、chibiasを呼び出します:

```bash
$ chibias -r /mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319/mscorlib.dll -o hello.exe hello.s
```

実行します:

```bash
$ ./hello.exe
Hello world with chibias!
```

このサンプルは、 `mscorlib` アセンブリに定義されている `System.Console.WriteLine()` を参照します。
そして、このファイルは Windows のシステムディレクトリに存在するものを、WSL環境から指定しているため、
少々わかりにくいかもしれません。
しかし、chibiasがどうやって参照するアセンブリを特定するのか、についてはこの例で十分でしょう。

Linuxや他のOSでも、必要な参照を追加することで同じように使うことができます。
また、純粋に計算だけのコードをアセンブルした場合は、他のアセンブリへの参照は必要ありません:

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

### 参考: どうやってmscorlibなどを入手するか?

`mscorlib.dll` ファイルを合法的に入手したい場合は、
nugetで公開されている、[ReferenceAssemblies (net45)](https://www.nuget.org/packages/microsoft.netframework.referenceassemblies.net45) パッケージを利用することができます。

このパッケージはMSがMITライセンスで提供しているものなので、自由に使うことができます。
nupkgファイルはzip形式なので、`unzip` を使って中身を取り出すことができます。

注意点として、本パッケージに含まれる全てのアセンブリは、コード本体を持ちません。
chibiasで参照することは可能ですが、実行することはできません。
Linux環境などで実行する場合は、mono/.NET Coreなどのランタイムが必要になります:

```bash
$ mono ./hello.exe
Hello world with chibias!
```

----

## chibiasアセンブリ構文

TODO: 作業中、まだ仕様が確定していません。

この章は随時更新していますが、現在の構文を確認するには、[テストコード](https://github.com/kekyo/chibias-cil/blob/main/chibias.core.Tests/AssemblerTests.cs) を参照して下さい。

chibiasの構文には、以下の特徴があります:

* オプコードの本体は、殆どILAsmと同様に記述が可能
* 不必要に冗長な記述を、可能な限り排除
* OOPに関係するコードは、記述できないか制限がある

基本的には、chibiccのように「C言語」を実現するために設計されていますが、
ILAsmと比較しても、はるかに簡単に書けるはずです。

### 最小でエントリーポイントのみ含む例

```
.function int32 main
    ldc.i4 123    ; これはコメントです
    ret
```

* ソースコードは、常にUTF-8で解釈されます。
* 行頭や行末の空白は無視されます。
  * つまり、インデントはすべて無視されます。
* セミコロン (';') はコメントの開始を意味します。行末までがすべてコメントとみなされます。
* ピリオド ('.') で始まる単語は、「アセンブラディレクティブ」とみなされます。
  * `.function` ディレクティブは、関数の開始を意味していて、戻り値の型名と関数名が続きます。
  * 次の関数ディレクティブが現れるまで、関数の本体が続きます。
* コマンドラインのオプションに `--exe` を指定するなどして、実行可能形式を生成する場合、関数名が `main` であれば、自動的にエントリポイントとみなされます。

### リテラル

```
.function int32 main
    ldc.i4 123
    ldc.r8 1.234
    ldstr "abc\"def\"ghi"
    pop
    pop
    ret
```

* 数値リテラル形式は、.NET format providerと互換性があります。
  * 整数リテラル: `System.Int32.Parse()` などに `InvariantCulture` を適用した場合と同様です。
  * 浮動小数点数リテラル: `System.Double.Parse()` などに `InvariantCulture` を適用した場合と同様です。
* 文字列リテラルは二重引用符（'"'）で囲みます。
  * エスケープ文字は ('\\') で、トライグラフシーケンス以外はC言語仕様と同じです。
  * 16進数 ('\\xnn')、UTF-16('\\unnn') が使用可能です。

### ラベル

```
.function int32 main
    ldc.i4 123
    br NAME
    nop
NAME:
    ret
```

ラベル名は (':') で終わります。
ラベル名は関数スコープ内で一意であることが必要です。

### ビルトイン型名

|型名|実際の型|エイリアス名|
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

配列・ポインタ・参照の組み合わせが可能です。
(空白で区切られたものは不可）

* `int[]`
* `int[][]`
* `int[4]`
* `int[4][3]`
* `int32*`
* `int32**`
* `int32&`

配列の要素数を指定した型は、「値型配列」と呼ばれ、.NET CLRの配列とは扱いが異なります。
詳細は、別項を参照してください。

### ローカル変数

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

関数本体の中で、`.local` ディレクティブを使って、ローカル変数を宣言することができます。
このディレクティブはオプションで変数名を指定することができます。

また、オプコードのオペランドで、変数名を使用して参照することができます:

```
.function void foo
    .local int32 abc
    ldc.i4 1
    stloc abc
    ret
```

### 別の関数を呼び出す

```
.function int32 main
    ldc.i4 1
    ldc.i4 2
    call add2
    ret
.function int32 add2 x:int32 y:int32
    ldarg 0
    ldarg y   ; パラメータ名で参照することができる
    add
    ret
```

パラメータの定義は任意です。フォーマットは:

* `int32`: 型名だけを指定する。
* `x:int32`: パラメータ名と型名を指定する。

関数名は、前方参照、後方参照のいずれも可能です。

重要: chibiasで定義された関数を呼び出す場合は、`call` オペランドに引数型リストを指定する必要はありません。
.NETのオーバーロードされたメソッドを呼び出す場合は、引数型リストが必要です。

### 外部アセンブリの関数の呼び出し

事前に `test.dll` を作っておきます。内容は以下の通りです:

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

その後、以下のように、上記アセンブリの関数を呼び出します:

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

関数（.NET CILメソッド）は、`C.text`という名前のクラス内に配置されます。
そのマッピングは:

* `int32 main` --> `public static int32 C.text::main()`
* `int32 add2 a:int32 b:int32` --> `public static int32 C.text::add2(int32 a, int32 b)`

疑似的にC#で記述すると (test.dll):

```csharp
namespace C;

public static class text
{
    public static int add2(int a, int b) => a + b;
}
```

疑似的にC#で記述すると (main.exe):

```csharp
extern alias test;
using test_text = test::C.text;

namespace C;

public static class text
{
    public static int main() => test_text::add2(1, 2);
}
```

このような関数のメタデータ配置規則を、"CABI (chibicc application binary interface) 仕様" と呼びます。

### 外部アセンブリの.NETメソッドの呼び出し

.NETのメソッドを呼び出す場合は、完全なメソッド名と引数型リストを指定します:

```
.function void main
    ldstr "Hello world"
    call System.Console.WriteLine string
    ret
```

指定するメソッドは public でなければならず、ジェネリックパラメータを持つメソッドを指定することはできません。
インスタンスメソッドも指定できますが、当然ながら `this` の参照が評価スタックにプッシュされる必要があります。

引数型リストは、メソッドのオーバーロードを特定するために使用されます。

.NETメソッドを参照するために、コマンドラインオプション `-r` で、メソッド定義を含むアセンブリを指定する必要があります。これは、最も標準的な `mscorlib.dll` や `System.Runtime.dll` にも当てはまります。

### コールサイト構文

コールサイトとは、`calli` オペコードで指定する、呼び出し対象メソッドのシグネチャ記述子です。

```
.function int32 main
    ldstr "123"
    ldftn System.Int32.Parse string
    calli int32 string
    ret
```

コールサイトには、戻り値の型と引数の型のリストを指定します。

### グローバル変数

グローバル変数の書式は、ローカル変数と同じです。
ただし、関数本体定義の外側に配置します:

```
.function int32 main
    ldc.i4 123
    stsfld foo
    ldsfld foo
    ret
.global int32 foo
```

グローバル変数名は、前方参照、後方参照のいずれも可能です。

グローバル変数は CABIに従って、`C.data` クラス内に配置されます。

疑似的にC#で記述すると:

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

### データの初期化

グローバル変数の宣言は、初期化データを含むことが出来ます:

```
.function int32 bar
    ldsfld foo
    ret
; int32 foo = 0x76543210
.global int32 foo 0x10 0x32 0x54 0x76
```

データはバイト単位で埋める必要があります。
また、配置されたデータは書き込み可能であるため、取り扱いには注意が必要です。

### 値型の配列

.NETには、値型のように振る舞う配列型がありません。
chibiasは、 `value array` 型を使ってこれを擬似的に実現することができます。
値型の配列は、C言語コンパイラの実現において非常に重要な役割を担っています。

値型の配列を使用するには、以下のように型を宣言します:

```
.function int8[5] bar   ; <-- 値型の配列には要素数が必要
    ldsfld foo
    ret
.global int8[5] foo 0x10 0x32 0x54 0x76 0x98
```

このとき、`bar` 関数の戻り値と `foo` 変数の型は、 `System.SByte_len5` 型になります。
具体的には、以下のような構造体型が自動的に宣言されます。

疑似的にC#で記述すると:

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

この構造体型は、chibias（とchibicc）の外では、配列のように振る舞うことができます。

また、型の自然な解釈も行われます。例えば:

* `int8[5]*` --> `System.SByte_len5*`
* `int8*[5]` --> `System.SByte_ptr_len5`
* `int8[5][3]` --> `System.SByte_len5_len3`
* `int8[5]*[3]` --> `System.SByte_len5_ptr_len3`

### 構造体型

chibiasで定義できる型は構造体型のみです。
つまり、`System.ValueType` を暗黙のうちに継承した型です。

```
.structure foo
    int32 a
    int8 b
    int32 c
```

疑似的にC#で記述すると:

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

デフォルトでは、構造体のパッキングはCLRに任されています。
明示的に指定する場合は:

```
.structure foo 4  ; pack=4
    int32 a
    int8 b
    int32 c
```

または各メンバーにオフセットを与えます:

```
.structure foo explicit
    int32 a 0     ; offset=0
    int8 b 4      ; offset=4
    int32 c 5     ; offset=5
```

オフセットを任意に調整することで、C言語における共用体型を再現することができます。

### 位置情報を明示する

`.file`と`.location`ディレクティブは、シーケンスポイントと呼ばれるデバッグ情報を生成します。
シーケンスポイントは、実行中のコードがソースコードのどこに対応するのかを検索するために使用されます。
つまり、この情報を与えると、デバッガが実行中のコードの位置を、ソースコード上で示すことが出来るようになります。

これらの情報は任意で、存在しなくてもアセンブル処理に影響はありません。

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

* `.file`は，IDをソースコード・ファイルに対応付けます。
  * 第1オペランド: ID (数値を含む任意のシンボル名。GAS の `.fil` ディレクティブと同じ)
  * 第2オペランド: ファイルパス（またはソースコードを識別する）文字列
  * 第3オペランド: 言語名称。以下のリストを参照(オプション)
  * `.file`ディレクティブは常に宣言することができ、同じIDを上書きします。
* `.location`ディレクティブは、ソースコードの位置を示します。
  * 第1オペランド: 参照するソースコード・ファイルのID。
  * 第2オペランド: 開始行のインデックス (0ベースのインデックス)
  * 第3オペランド: 開始桁のインデックス (0ベースのインデックス)
  * 第4オペランド: 終了行のインデックス (0ベースのインデックス、かつ開始行以上)
  * 第5オペランド: 終了桁のインデックス (0ベースのインデックス、かつ開始桁より大きい)
  * `.location`ディレクティブは、関数本体でのみ宣言可能です。

言語名称の例（すべてではありません）:

|言語名称|言語|
|:----|:----|
|`cil`|CIL|
|`c`|C|
|`cpp`|C++|
|`csharp`|C#|
|`fsharp`|F#|
|`other`|-|

言語名称は、[Mono.Cecil.Cil.DocumentLanguage](https://github.com/jbevain/cecil/blob/7b8ee049a151204997eecf587c69acc2f67c8405/Mono.Cecil.Cil/Document.cs#L27) に由来しています。

これらのディレクティブを適用しなかった場合は、CILソースファイル自身を示すようなデバッグ情報を生成します。

----

## TODO

実装されるかもしれない:

* `OperandType`
  * InlineSwitch
* Handling function/global variable scopes.
* Automatic implements `IList<T>` on value array type.
* Handling variable arguments.
* Handling method optional attributes (inline, no-inline and no-optimizing?)
* Handling for target framework moniker.
  * Refers `System.Object` from `C.text` base class, is it referenced to `mscorlib` or `System.Runtime` ?
* Generate CIL `Main(args)` handler and bypass to C specific `main(argc, argv)` function.
* And chibicc-cil specific requirements...

実装されない可能性がある:

* `OperandType`
  * InlinePhi
* Handling multi-dimensional array types.
* Exception handling.
* And NOT chibicc-cil specific requirements.


----

## chibiasの背景

当初は [ILAsm.Managed](https://github.com/kekyo/ILAsm.Managed) を使っていたのですが、いくつか問題点がありました:

* アセンブリ参照の解決。
  ILAsm では、外部アセンブリで定義されたメンバを参照する場合、明示的にアセンブリ名を修飾する必要があります。
  しかし、コンパイラ内部でこれを解決するのは簡単ではありません。
* `.maxstack`の計算。
  評価スタック全体の消費量を測定するために、コンパイラ内部で実行フローを解析する必要があります。
* デバッグ情報の提供
  ILAsmはデバッグ情報を提供することができますが、以下のような問題点があります。
  * CILソースコード自身を参照するようなデバッグ情報は得ることができるが、元のソースコードへの参照を作成することはできない。
    これは、GNUアセンブラにおける `.fil` と `.loc` ディレクティブに相当します。
    また、.NET環境では、より豊富なデバッグ情報を提供できる可能性がありましたが、ILAsmを使う限りは不可能です。
  * マネージドデバッグ情報は「MDB」形式であり、現代では適しません。
    "Portable PDB" や "Embedded" で出力できることが必要です。
    MSが提供するILAsmを使えば解決するのですが、[別の問題](https://github.com/kekyo/ILAsm.Managed#background)があります。
* ILAsmは構文規則が複雑で冗長です。
  * 過剰なカーリーブレイス構文 ('{ ... }') をなくしたい。
  * 殆ど使用されない、オプショナルなメンバー属性をなくしたい。
  * 名前空間名やメンバー名の過剰な修飾をなくしたい。

ILAsmを使いながらこれらを回避するのは難しいと判断し、必要最低限の機能を備えた独自のアセンブラを実装することになりました。

chibicc-cilの移植と並行して実装を行っていますが、その際に新たに見出した機能の追加や変更を行っています。例えば:

* 値型の配列機能を追加
* グローバル変数の初期化機能を統合
* CABIの定義

----

## License

Under MIT.
