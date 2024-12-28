﻿# The specialized backend toolchain for chibicc-cil

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)

## NuGet

### .NET CLI interface

| Package     | NuGet                                                                                                                            |
|:------------|:---------------------------------------------------------------------------------------------------------------------------------|
| chibiar-cli | [![NuGet chibiar-cli](https://img.shields.io/nuget/v/chibiar-cli.svg?style=flat)](https://www.nuget.org/packages/chibiar-cli)    |
| chibias-cli | [![NuGet chibias-cli](https://img.shields.io/nuget/v/chibias-cli.svg?style=flat)](https://www.nuget.org/packages/chibias-cli)    |
| chibild-cli | [![NuGet chibild-cli](https://img.shields.io/nuget/v/chibild-cli.svg?style=flat)](https://www.nuget.org/packages/chibild-cli)    |

### Core libraries

| Package                  | NuGet                                                                                                                            |
|:-------------------------|:---------------------------------------------------------------------------------------------------------------------------------|
| chibiar.core             | [![NuGet chibiar.core](https://img.shields.io/nuget/v/chibiar.core.svg?style=flat)](https://www.nuget.org/packages/chibiar.core) |
| chibias.core             | [![NuGet chibias.core](https://img.shields.io/nuget/v/chibias.core.svg?style=flat)](https://www.nuget.org/packages/chibias.core) |
| chibild.core             | [![NuGet chibild.core](https://img.shields.io/nuget/v/chibild.core.svg?style=flat)](https://www.nuget.org/packages/chibild.core) |
| chibicc.toolchain.common | [![NuGet chibicc.toolchain.common](https://img.shields.io/nuget/v/chibicc.toolchain.common.svg?style=flat)](https://www.nuget.org/packages/chibicc.toolchain.common) |

[English language is here](https://github.com/kekyo/chibicc-cil-toolchain)

## これは何?

chibicc-toolchainは、.NET CIL/CLR 上に [chibicc](https://github.com/rui314/chibicc) を移植するためのバックエンドとなる、CIL/MSILアセンブラ・リンカなどのツールチェインです。
このツールチェインで使用するCILアセンブラは、簡素な構文規則でCILアセンブリ言語を記述可能で、アセンブルを行い、.NETアセンブリファイルを出力することが出来ます。
以下はコード例です:

```
.function public void() main
    ldstr "Hello world"
    call void(string) System.Console.WriteLine
    ret
```

このツールチェインは、表面的には単なるCILアセンブラのバリエーションの一つとみなすことが出来ますが、
.NET標準のILAsmがCILアセンブラとして網羅的かつプリミティブなツールチェインである一方で、
chibicc-toolchainはC言語との相互運用性を向上させるための、いくつかの重要なフィーチャーを盛り込んでいる点が異なります。

まだ作業中ですが、[YouTube](https://bit.ly/3XbqPSQ) にて、Gitコミット毎に移植する解説シリーズを放送しています。

[chibicc-cil](https://github.com/kekyo/chibicc-cil) は、ある程度移植が進んだ段階で公開する予定です。しばらくお待ちください。

----

## chibildの全体像

chibicc-toolchainは、以下のツールチェインプログラムで構成されています:

* chibias: CILアセンブラ
* chibild: CILオブジェクトリンカ
* chibiar: CILオブジェクトアーカイバ

![chibicc-toolchain overview](Images/toolchain.png)

chibiasは、CILソースコード群('*.s')をほぼそのままオブジェクトファイル('*.o')として出力し、
CILソースコードの実質的なアセンブル処理は、chibildが行います。

* chibiasは、パーサーによる構文チェックを行いますが、シンボルの妥当性など細かい検査を行いません。
* chibiasが出力するオブジェクトファイルは、実際には入力となるCILソースファイルをgzipで圧縮しただけです。
  このことは、実際にオブジェクトファイルを `gzip -d` することで確かめることができます。
* 同様に、chibiarが出力するアーカイブファイルは、実際にはシンボルテーブルファイルを含めたzipファイルです。
  全く同様に、`unzip` コマンドで確かめることができます。

このような、chibias, chibild, chibiarの奇妙な挙動は実装の制約によるものですが、
ツールチェイン使用者にとって見れば、POSIXで想定されるas,ld,arなどのツールチェインと対比させて使用方法を理解できるという強みがあります。

以降の説明では、CILアセンブリソースコードをchibildで直接使用して解説します。

### CILアセンブル処理

chibildは、複数のCILオブジェクトファイル（またはCILソースファイル）を入力として、アセンブルを行い、
結果を.NETアセンブリとして出力します。
この時、参照アセンブリ群を指定して、オブジェクトファイルから参照出来るようにします。

chibildはchibiccのバックエンドアセンブラとして開発しましたが、単独でも使用可能です。
ソースコードは、ILAsmと比べて簡素化された構文規則を採用するため、機械出力を行いやすく、
かつ人間にとっても書きやすくなっています。


----

## 使用方法

CLIバージョンのツールチェインを、nugetからインストール出来ます

* chibias: [chibias-cli](https://www.nuget.org/packages/chibias-cli)
* chibild: [chibild-cli](https://www.nuget.org/packages/chibild-cli).
* chibiar: [chibiar-cli](https://www.nuget.org/packages/chibiar-cli).

(紛らわしいのですが、 'chibias-cil' ではありません :)

```bash
$ dotnet tool install -g chibias-cli
$ dotnet tool install -g chibild-cli
$ dotnet tool install -g chibiar-cli
```

使用可能になったかどうかは、以下のように確認できます:

```bash
$ cil-ecma-chibild

cil-ecma-chibild [0.63.0,net6.0] [...]
This is the CIL object linker, part of chibicc-cil project.
https://github.com/kekyo/chibicc-cil-toolchain
Copyright (c) Kouji Matsui
License under MIT

usage: cil-ecma-chibild [options] <input path> [<input path> ...]
  -o <path>         Output assembly path
  -shared, -mdll    Produce dll assembly
           -mexe    Produce executable assembly (defaulted)
           -mwinexe Produce Windows executable assembly
  -L <path>         Reference assembly base path
  -l <name>         Reference assembly name
  -i <path>         Will inject into an assembly file
  -g, -g2           Produce embedded debug symbol (defaulted)
      -g1           Produce portable debug symbol file
      -gm           Produce mono debug symbol file
      -gw           Produce windows proprietary debug symbol file
  -s, -g0           Omit debug symbol file
  -O, -O1           Apply optimization
      -O0           Disable optimization (defaulted)
  -x                Will not copy required assemblies
  -d <path>         CABI startup object directory path
  -e <symbol>       Entry point symbol (defaulted: _start)
  -v <version>      Apply assembly version (defaulted: 1.0.0.0)
  -m <tfm>          Target framework moniker (defaulted: net6.0)
  -m <arch>         Target Windows architecture [AnyCPU|Preferred32Bit|X86|X64|IA64|ARM|ARMv7|ARM64] (defaulted: AnyCPU)
  -m <rollforward>  CoreCLR rollforward configuration [Major|Minor|Feature|Patch|LatestMajor|LatestMinor|LatestFeature|LatestPatch|Disable|Default|Omit] (defaulted: Major)
  -a <path>         CoreCLR AppHost template path
      --log <level> Log level [debug|trace|information|warning|error|silent] (defaulted: warning)
      --dryrun      Need to dryrun
  -h, --help        Show this help
```

* chibildの実際のコマンド名は、`cil-ecma-chibild` です。chibiarやchibiasも同様です。
  この命名規則は、GNU binutilsになぞらえたものです。
* chibildは、コマンドラインで指示された複数の入力ファイル（オブジェクト '*.o'、CILソース '*.s'）
  をアセンブルして、1つの.NETアセンブリにまとめます。
* 参照ライブラリ名 `-l` は、アーカイブファイルパス ('*.a') を含めて、先頭から順に評価されます。
  この機能は、重複するシンボル(関数/グローバル変数)にも適用されます。
  また、ライブラリファイル名は、ネイティブツールチェインと同様に `lib` というプレフィックスが適用されている事を想定し、
  かつ、フォールバックとして指定されたままのファイル名も想定します。
* ターゲットフレームワークのデフォルト(上記の例では`net6.0`)は、chibildの動作環境に依存します。
* ターゲットフレームワークの指定は、コアライブラリのバリエーションを仮定するだけで、
  `mscorlib.dll`や`System.Private.CoreLib.dll`アセンブリを自動的に検出するわけではありません（別章参照）。
* ターゲットWindowsアーキテクチャは、デフォルトで`AnyCPU`です。大文字小文字は無視されます。
  この値は、アセンブリにマークを設定するだけです。異なる値を指定したとしても、生成されるオプコードには影響ありません。
  WindowsのCLR環境以外では、常に`AnyCPU`として動作する可能性があります。
* ログレベルは、デフォルトで`warning`です。大文字小文字は無視されます。
* 注入モード `-i` を使用する場合は、 `-a`, `-v`, `-m` の指定が無視されます。
* chibildには、システムアセンブリ（`mscorlib.dll`や`System.Private.CoreLib.dll`など）を除く、
  参照されたアセンブリを自動的にコピーする機能がありますが、`-x`を指定すると、自動的にコピーしません。
  この機能は簡易的なものであり、必要な全てのファイルをコピーしない場合があるため、注意が必要です。
  複雑なデプロイを前提とする場合は、MSBuildによるビルドが必要になると思われます。


----

## Hello world

chibildを使って "Hello world" を実行してみましょう。
新しいソースコード・ファイル `hello.s` を作り、以下のようにコードを書きます。この4行だけでOKです:

```
.function public void() _start
    ldstr "Hello world with chibild!"
    call void(string) System.Console.WriteLine
    ret
```

出来たら、chibildを呼び出します:

```bash
$ cil-ecma-chibild -mnet45 -L/mnt/c/Windows/Microsoft.NET/Framework64/v4.0.30319 -lmscorlib -o hello.exe hello.s
```

実行します:

```bash
$ ./hello.exe
Hello world with chibild!
```

このサンプルは、 `mscorlib.dll` アセンブリに定義されている `System.Console.WriteLine()` を参照します。
そして、このファイルは Windows のシステムディレクトリに存在するものを、WSL環境から指定しているため、
少々わかりにくいかもしれません。
しかし、chibildがどうやって参照するアセンブリを特定するのか、についてはこの例で十分でしょう。

Linuxや他のOSでも、必要な参照を追加することで同じように使うことができます。
また、ビルトイン型（後述）だけを使用するコードをアセンブルした場合は、他のアセンブリへの参照は必要ありません:

```
.function public int32() _start
    ldc.i4.1
    ldc.i4.2
    add
    ret
```

```bash
$ cil-ecma-chibild -mnet45 -o adder.exe adder.s
$ ./adder.exe
$ echo $?
3
```

* 注意: この例では、アセンブル時に属性に関する警告が発生しますが、無視して構いません。
* `main` 関数の `argc` や `argv` に相当する引数を使用する場合は、
  スタートアップコード(`crt0.o`)の指定と、FCLである `mscorlib.dll` アセンブリへの参照が必要です。

### .NET Coreなどで動かすには

ターゲットフレームワークを指定して、かつ参照アセンブリに`System.Private.CoreLib.dll`が含まれるようにします:

```bash
$ cil-ecma-chibild -mnet6.0 -L$HOME/.dotnet/shared/Microsoft.NETCore.App/6.0.13 -lSystem.Private.CoreLib \
  -o hello.exe hello.s
```

ターゲットフレームワークと、対応するコアライブラリのバージョンは一致する必要があります。
`net6.0`と指定して、.NET Core 2.2のコアライブラリを使用すると、警告が表示されます。

注意: 現在のところ、マイナーなターゲットフレームワークには対応していません。
例えば、`uap10.0`、`tizen1`や`portable+net45+wp5+sl5`などです。
標準となる、ターゲットフレームワークのパーサーが、BCLに含まれていない事が理由です。

### 参考: どうやってコアアセンブリファイルを入手するか?

`mscorlib.dll` ファイルを合法的に入手したい場合は、
nugetで公開されている、[ReferenceAssemblies (net45)](https://www.nuget.org/packages/microsoft.netframework.referenceassemblies.net45) パッケージを利用することができます。

このパッケージはMSがMITライセンスで提供しているものなので、自由に使うことができます。
nupkgファイルはzip形式なので、`unzip` を使って中身を取り出すことができます。

注意点として、本パッケージに含まれる全てのアセンブリは、コード本体を持ちません。
chibildで参照することは可能ですが、実行することはできません。
Linux環境などで実行する場合は、mono/.NET Coreなどのランタイムが必要になります:

```bash
$ mono ./hello.exe
Hello world with chibild!
```

いずれにしても、完全な `mscorlib.dll`や`System.Private.CoreLib.dll`ファイルを参照したい場合は、
単にmonoや.NET SDKをインストールして、そのディレクトリ内のファイルを参照するほうが良いかもしれません。

今のところ、chibildは、システムにインストールされたこれらのアセンブリファイルを自動的に検出しません。
GNUアセンブラのように、単体で独立したアセンブラとして、意図的に設計した結果です。
将来的には、MSBuildスクリプトを経由して、自動的にアセンブリファイルを解決出来るようになるかもしれません。

(この目的のために、`chibicc.build` パッケージを用意しています。このパッケージはまだ不完全で使用できません。)


----

## chibildアセンブリ構文

TODO: 作業中、まだ仕様が確定していません。

この章は随時更新していますが、現在の構文を確認するには、[テストコード](https://github.com/kekyo/chibicc-cil-toolchain/blob/main/chibild.core.Tests/AssemblerTests.cs) を参照して下さい。

chibildの構文には、以下の特徴があります:

* オプコードの本体は、殆どILAsmと同様に記述が可能
* 不必要に冗長な記述を、可能な限り排除
* OOPに関係するコードは、記述できないか制限がある

基本的には、chibiccのように「C言語」を実現するために設計されていますが、
ILAsmと比較しても、はるかに簡単に書けるはずです。

### 最小でエントリーポイントのみ含む例

```
.function public int32() _start
    ldc.i4 123    ; これはコメントです
    ret
```

* ソースコードは、常にUTF-8で解釈されます。
* 行頭や行末の空白は無視されます。
  * つまり、インデントはすべて無視されます。
* セミコロン (';') はコメントの開始を意味します。行末までがすべてコメントとみなされます。
* ピリオド ('.') で始まる単語は、「アセンブラディレクティブ」とみなされます。
  * `.function` ディレクティブは、関数の開始を意味しています。以下の順にオペランドが続きます:
    * スコープ記述子
    * 関数のシグネチャ
    * 関数名
  * 次の関数ディレクティブが現れるまで、関数の本体が続きます。

スコープ記述子は、他の宣言でも共通です。

|スコープ記述子|内容|
|:----|:----|
|`public`|どのスコープからも参照可能。外部アセンブリからでも参照可能。|
|`internal`|同じアセンブリ内でのみ参照可能。|
|`file`|現在のソースコードファイルからのみ参照可能。|

* コマンドラインのオプションに `-mexe` を指定するなどして、実行可能形式を生成する場合、
  関数名が `_start` であれば、自動的にエントリポイントとみなされます。
* `-efoo` と指定すると、エントリポイントのシンボルを `foo` に変更することが出来ます。

エントリポイントのシグネチャは、CLRの要求に従います。つまり、以下の組み合わせが可能です:

|関数シグネチャ|対応するC#でのシグネチャ例|
|:----|:----|
|`void()`|`static void _start()`|
|`int32()`|`static int32 _start()`|
|`void(string[])`|`static void _start(string[] args)`|
|`int32(string[])`|`static int32 _start(string[] args)`|

* 関数シグネチャについては、後述の解説を参照してください。

#### 補足: スタートアップコード

chibiccを使う場合、エントリポイントはあくまでC言語の `main` 関数として定義する事を想定しています。
例えば、 `int main(int argc, char **argv)` です。

このシグネチャの違いは、スタートアップコードを含む `crt0.o` のようなオブジェクトファイルをchibiccが供給し、
その中でC言語で必要な引数を生成することによって実現しています。
（この目的のために、特殊な `-d` オプションが存在します）

このドキュメントでは、スタートアップコードを省いて解説しています。
従って、スタートアップコードの規則は、常に上記の表に従います。

### リテラル

```
.function public int32() _start
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
  * 16進数 ('\\xnn')、UTF-16('\\unnnn') が使用可能です。

### ラベル

```
.function public int32() _start
    ldc.i4 123
    br NAME
    nop
NAME:
    ret
```

ラベル名は (':') で終わります。
ラベル名は関数スコープ内で一意であることが必要です。

### 型名

型名は、ビルトイン型と.NETの型名の両方を指定出来ます。
.NETの型名は、常に名前空間を含む完全限定名で指定します。
例えば、`System.String`のような形式です。

ビルトイン型は以下の通りです:

|ビルトイン型|実際の型|エイリアス名|
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
|`nint`|`System.IntPtr`|`intptr`|
|`nuint`|`System.UIntPtr`|`uintptr`|
|`bool`|`System.Boolean`| |
|`char`|`System.Char`|`char16`|
|`object`|`System.Object`| |
|`string`|`System.String`| |
|`typedref`|`System.TypedReference`| |

ビルトイン型のうち、`System.Boolean`型と`System.Char`型は特殊で、chibildがこれらの型を使用する場合、
常に1バイト又は2バイトマーシャリングを適用します。
.NETのデフォルトでは、これらの型のフットプリントサイズは、状況によって変化しますが、
chibildを使って生成されるアセンブリは、常に上記のサイズとなる事を意味します。

関数ポインタ型は、以下のように指定します:
(空白で区切られたものは不可）

```
string(int8,int32)*
```

疑似的にC#で記述すると:

```csharp
// スタティックメソッドの例
static string foo(sbyte a, int b)

// 関数デリゲートの場合
unsafe delegate*<sbyte, int, string>
```

また、可変引数を持つ関数のポインタ型も、`...` を使用して表現できます:

```
string(int8,int32,...)*
```

配列・ポインタ・参照（マネージポインタ）の組み合わせが可能です。

* `int32[]`
* `int32[][]`
* `int32[4]`
* `int32[4][3]`
* `int32*`
* `int32**`
* `int32&`
* `string(int32&,int8)*[42]`
* `int(sbyte*)*(string,int8)*`

配列の要素数を指定した型は、「値型配列」と呼ばれます。

関数ポインタ型、可変引数、値型配列のそれぞれは、C#においてのデリゲート型、
`params`による可変引数、`System.Array`を基底とした配列とは扱いが異なります。
詳細は、別項を参照してください。

### ローカル変数

```
.function public int32() _start
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
.function public void() foo
    .local int32 abc
    ldc.i4 1
    stloc abc
    ret
```

ローカル変数の型が、参照（マネージポインタ）である場合は、自動的に `pinned` とマークされます。
これにより、値型へのポインタを扱う際の、スクラッチバッファとして使用できます:

```
.function public int32() foo
    .local int32& buf    ; <-- pinned
    .local int32* p
    ldsflda gv
    stloc.0
    ldloc.0
    conv.u
    stloc.1
    ldloc.1
    ldind.i4
    ret
.global int32 gv
```

### 別の関数を呼び出す

```
.function public int32() _start
    ldc.i4 1
    ldc.i4 2
    call add2
    ret
.function public int32(x:int32,y:int32) add2 
    ldarg 0
    ldarg y   ; パラメータ名で参照することができる
    add
    ret
```

関数シグネチャのパラメータの定義は任意です。フォーマットは:

* `int32`: 型名だけを指定する。
* `x:int32`: パラメータ名と型名を指定する。

関数名は、前方参照、後方参照のいずれも可能です。

重要: chibildで定義された関数を呼び出す場合は、`call` オペランドに引数型リストを指定する必要はありません。
.NETのオーバーロードされたメソッドを呼び出す場合は、引数型リストが必要です。

関数の引数で、追加の可変引数を受け取ることが出来ます:

```
.function public int32(count:int32,...) add_n
    .local System.ArgIterator
    ldloca.s 0
    arglist
    call void(System.RuntimeArgumentHandle) System.ArgIterator..ctor
    ; (ArgIteratorを使って列挙する)
    ret
```

関数シグネチャの引数リストの終端に `...` を指定する事で、可変引数を受け取るようにマークされます。

但し、この可変引数は、C#における可変引数とは扱いが異なることに注意してください。
C#では、可変引数を.NET配列で受け取りますが、chibildではCILの`arglist`と呼ばれる機能を使います。

この可変引数は、上記例のように、`System.ArgIterator`型を用いて列挙を行います。
詳しくは、C#の`__arglist`キーワードや`ArgIterator`で調べて下さい。

可変引数を持つ関数の呼び出しは、引数の型リストを明示する必要があります。
例えば、上記の`add_n`を呼び出す場合は:

```
.function public int32() _start
    ldc.i4.s 123
    ldc.r8 123.456    ; <-- 追加引数
    ldstr "ABC"       ; <-- 追加引数
    call int32(int32,float64,string) add_n
    ret
```

追加引数に相当する型を含めた、関数呼び出しで渡すべきパラメータのすべての型を、シグネチャとして明示します。
chibildはオプコードのフロー解析を行わないため、この指定が誤っていると、可変引数を持つ関数の呼び出しが実行時に失敗します。

### 外部アセンブリの関数の呼び出し

事前に `test.dll` を作っておきます。内容は以下の通りです:

```
.function public int32(a:int32,b:int32) add2
    ldarg 0
    ldarg 1
    add
    ret
```

```bash
$ chibild -c test.s
```

その後、以下のように、上記アセンブリの関数を呼び出します:

```
.function public int32() _start
    ldc.i4 1
    ldc.i4 2
    call add2
    ret
```

```bash
$ chibild -ltest main.s
```

関数（.NET CILメソッド）は、`C.text`という名前のクラス内に配置されます。
そのマッピングは:

* `int32() _start` --> `public static int32 C.text::main()`
* `int32(a:int32,b:int32) add2` --> `public static int32 C.text::add2(int32 a, int32 b)`

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

CABIが適用されるのは、外部アセンブリから参照可能な場合のみです。
関数のスコープが `public` ではない場合は、外部のアセンブリから参照出来ず、CABI準拠ではなくなります。

### 外部アセンブリの.NETメソッドの呼び出し

.NETのメソッドを呼び出す場合は、メソッドのシグネチャと完全なメソッド名を指定します:

```
.function public void() main
    ldstr "Hello world"
    call void(string) System.Console.WriteLine
    ret
```

指定する.NETのメソッドは `public` でなければならず、ジェネリックパラメータを持つメソッドを指定することはできません。
インスタンスメソッドも指定できますが、メソッドシグネチャの引数に、 `this` の型は記述しません。
当然ながら `this` の参照が評価スタックにプッシュされる必要があります。

メソッドのシグネチャは、メソッドのオーバーロードを特定するために使用されます。
シグネチャを指定せず、オーバーロードメソッドが複数存在する場合は、誤ったメソッドを選択する可能性があります。

.NETメソッドを参照するために、コマンドラインオプション `-l` で、メソッド定義を含むアセンブリを指定する必要があります。
これは、最も標準的な `mscorlib.dll` や `System.Runtime.dll` にも当てはまります。

補足: プロパティやインデクサを呼び出す必要がある場合は、それらを実装するメソッドのシグネチャを特定しておく必要があります。例えば:

```
    ldstr "ABCDE"
    call System.String.get_Length   ; "ABCDE".Length
```

殆どのプロパティは慣例で、決まった命名規則を使います。上記のように、`System.String.Length`の場合は、
getterに`get_Length()`、setterに`set_Length()`というメソッド名に対応します。
また、インデクサの場合は、`get_Item()`や`set_Item()`というメソッド名に対応します。

但し、これらの命名規則は強制ではないため、異なる名前を適用している可能性があります。
うまくいかない場合は、ILDAsmやILSpy等のツールを使って確認して下さい。

### 関数シグネチャ構文

関数シグネチャは、呼び出し対象メソッドの引数群と戻り値の型を示す構文です。
.NETでは、コールサイトと呼ばれる場合もあります。
chibildでは、関数ポインタ型と同じような構文で指定します。

関数ポインタ型と異なるのは、関数シグネチャはポインタではないため、終端に`*`が付かない事です。

* `int32(string)*`: 関数ポインタ
* `int32(string)`: 関数シグネチャ（コールサイト）

これらは、関数ディレクティブや、`call` や `ldftn` オプコードでメソッドのオーバーロードを特定したり、
`calli` オプコードで使用します:

```
.function public int32() _start
    ldstr "123"
    ldftn int32(string) System.Int32.Parse
    calli int32(string)
    ret
```

### グローバル変数

グローバル変数の書式は、ローカル変数の書式にスコープ記述子を加えたものです。
ただし、関数本体定義の外側に配置します:

```
.function public int32() _start
    ldc.i4 123
    stsfld foo
    ldsfld foo
    ret
.global public int32 foo
```

グローバル変数名は、前方参照、後方参照のいずれも可能です。

グローバル変数は `public` の場合、CABIに従って、`C.data` クラス内に配置されます。

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
.function public int32() bar
    ldsfld foo
    ret
; int32 foo = 0x76543210
.global internal int32 foo 0x10 0x32 0x54 0x76
```

データはバイト単位で埋める必要があります。

初期化データを含んだ場合、そのメモリ領域に値を書こうとすると、`AccessViolationException`が発生する可能性があります。

### イニシャライザ

イニシャライザは、アセンブリのデータを操作する直前に実行される、関数の一種です。
内部的には、.NETのタイプイニシャライザから呼び出されます。
主に、グローバル変数の複雑な初期化処理に用いることが出来ます:

```
.initializer internal
    ldc.i4 123
    stsfld foo
    ret
.global public int32 foo
```

イニシャライザのスコープ記述子は、`public`と指定しても、`internal`と同じとみなされます。
また、スコープを定めるだけではなく、初期化が実行される契機も決定します:

* `public`又は`internal`: `public`又は`internal`のスコープが指定されたグローバル変数を操作する直前に呼び出される。
* `file`: `file`スコープが指定されたグローバル変数を操作する直前に呼び出される。

### 値型の配列

.NETには、値型のように振る舞う配列型がありません。
chibildは、 `value array` 型を使ってこれを擬似的に実現することができます。
値型の配列は、C言語コンパイラの実現において非常に重要な役割を担っています。

値型の配列を使用するには、以下のように型を宣言します:

```
.function public int8[5]() bar   ; <-- 値型の配列には要素数が必要
    ldsfld foo
    ret
.global internal int8[5] foo 0x10 0x32 0x54 0x76 0x98
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

この構造体型は、chibild（とchibicc）の外では、配列のように振る舞うことができます。

また、複合された型の自然な解釈も行われます。例えば:

* `int8[5]*` --> `System.SByte_len5*`
* `int8*[5]` --> `System.SByte_ptr_len5`
* `int8[5][3]` --> `System.SByte_len5_len3`
* `int8[5]*[3]` --> `System.SByte_len5_ptr_len3`

ネストした配列型を宣言する場合は、要素の順序に注意が必要です。
例えば、C言語で以下のように表現される型は、chibildでは要素の順序が逆になります:

```c
// C言語
char foo[3][4][5];

// chibild
int8[5][4][3] foo
```

これは、chibildが配列型を以下のように、左から右へと評価するためです。前述の複合された型定義も同様です:

`( ( int8 [5] ) [4] ) [3]`

注意: 値型配列を使用するには、`System.ValueType` 型と `System.IndexOutOfRangeException` 型への参照が解決される必要があります。
`mscorlib.dll` 又は `System.Private.CoreLib.dll` への参照を追加して下さい。

### 列挙体型

chibildで定義できる列挙体型は、.NETでの列挙体型と同様で、`System.Enum` を暗黙のうちに継承した型です。

```
.enumeration public int32 foo
    beef
    pork
    chicken
```

疑似的にC#で記述すると:

```csharp
namespace C.type;

public enum foo : int
{
    beef,
    pork,
    chicken
}
```

デフォルトでは、列挙値に割り当てられる値は、0から順にインクリメントされた値となります。
明示的に指定する場合は:

```
.enumeration public int32 foo
    beef 5
    pork 13
    chicken 42
```

列挙体型は、元となる数値の型を変更することが出来ます:

```
.enumeration public int64 foo   ; 値をint64として定義
    beef 5
    pork 13
    chicken 42
```

元となる型には、以下の型のみ指定可能です:

* `int8`, `uint8`
* `int16`, `uint16`
* `int32`, `uint32`
* `int64`, `uint64`

注意: 列挙体型を使用するには、`System.Enum` 型への参照が解決される必要があります。
`mscorlib.dll` 又は `System.Private.CoreLib.dll` への参照を追加して下さい。

### 構造体型

chibildで定義できる構造体型は、.NETでの構造体型と同様で、`System.ValueType` を暗黙のうちに継承した型です。

```
.structure public foo
    public int32 a
    internal int8 b
    public int32 c
```

疑似的にC#で記述すると:

```csharp
namespace C.type;

[StructLayout(LayoutKind.Sequential)]
public struct foo
{
    public int a;
    internal sbyte b;
    public int c;
}
```

構造体の各メンバーには、スコープ記述子が指定出来ます。
但し、`public` と `internal` のみで、`file` は指定出来ません。

デフォルトでは、構造体のパッキングはCLRに任されています。
明示的に指定する場合は:

```
.structure public foo 4  ; pack=4
    public int32 a
    public int8 b
    public int32 c
```

または各メンバーにオフセットを与えます:

```
.structure public foo explicit
    public int32 a 0     ; offset=0
    public int8 b 4      ; offset=4
    public int32 c 5     ; offset=5
```

オフセットを任意に調整することで、C言語における共用体型を再現することができます。

構造体型の最後のメンバーには、要素数を指定しない値型配列を指定することが出来ます:

```
.structure public foo
    public int32 a
    public int8 b
    public int32[*] c
```

配列の要素数を`*`と指定すると、この値型配列は要素数を特定しない型（フレックス配列）となります。
この型は特殊な値型配列で、配列のインデクサアクセスは、要素数の範囲外チェックが行われません。
当然、存在しない要素にアクセスした場合の結果は未定義となるため、注意が必要です。

フレックス配列は、構造体型の最後のメンバーに使用することを想定していますが、
chibildはこのチェックを行いません。

注意: 構造体型を使用するには、`System.ValueType` 型への参照が解決される必要があります。
`mscorlib.dll` 又は `System.Private.CoreLib.dll` への参照を追加して下さい。

### シンボルの重複について

chibildは、任意の型・グローバル変数・関数（これらをメンバーと呼称します）を検索する際に、以下のように検索を実行します:

1. 同一のオブジェクトファイル内に定義された、`file` スコープのメンバーが見つかれば、それを参照します。
2. 異なる入力に定義された、 `public` または `internal` スコープのメンバーが見つかれば、それを参照します。
   この時、検索の優先順位は、コマンドラインオプションに指定した入力群（オブジェクトファイル・アーカイブファイル・アセンブリファイル）の順に検索されます。
3. 見つからないため、エラーが発生します。

メンバーのシンボル名は、それぞれのメンバー種別に応じて検索されます。
シンボル名が同一でも、型・グローバル変数・関数は、それぞれ別として扱われます。

### 位置情報を明示する

`.file`と`.location`ディレクティブは、シーケンスポイントと呼ばれるデバッグ情報を生成します。
シーケンスポイントは、実行中のコードがソースコードのどこに対応するのかを検索するために使用されます。
つまり、この情報を与えると、デバッガが実行中のコードの位置を、ソースコード上で示すことが出来るようになります。

これらの情報は任意で、存在しなくてもアセンブル処理に影響はありません。

```
.file 1 "/home/kouji/Projects/test.c" c
.function public int32() _start
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
  * 第1オペランド: ID (数値を含む任意のシンボル名。GNUアセンブラの `.fil` ディレクティブと同じ)
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
  * `.location`ディレクティブを指定した直後のオプコードにのみ、適用されます。

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
しかし、`.hidden`ディレクティブを使用すると、以降のコードでシーケンスポイントを生成しなくなります:

```
.function public int32() _start
    .hidden
    ldc.i4 123     ; <-- シーケンスポイントは出力されない
    ldc.i4 456
    add
    ret
```

このような場合でも、再び有効なIDを指定する`.location`ディレクティブを指定すれば、シーケンスポイントを出力する事に注意して下さい。


----

## 注入モード

注入モードは、chibildの特徴的な機能の一つで、あらかじめ用意された .NETアセンブリファイル内に、直接CILコードを埋め込むモードです。
このモードは、コマンドラインオプションで `-i` を指定する事で有効化されます。

例えば、C#でコンパイルされた .NETアセンブリ `managed.dll` が存在する場合、
注入モードを使用することで、 `managed.dll` 内にアセンブルした結果のCILコードを直接埋め込むことが出来ます。

以下のようなC#ソースから生成した `managed.dll` アセンブリが存在するとします:

```csharp
namespace C;

// CILコード側から認識できるように、C.textクラス内にメソッドを配置する
public static class text
{
    public static long add_int64(long a, long b) =>
        a + b;
}
```

注入モードを使用すると、以下のCILコードを直接 `managed.dll` 内に「注入」して、`add_int64` と正しくリンク出来ます:

```
.function public int32() add_c
    ldc.i4.1
    conv.i8
    ldc.i4.2
    conv.i8
    call add_int64    ; C#側のメソッドを呼び出す
    conv.i4
    ret
```

以下のコマンドを使用して `injected.dll` を生成します:

```bash
$ cil-ecma-chibild -mnet6.0 -L$HOME/.dotnet/shared/Microsoft.NETCore.App/6.0.13 -lSystem.Private.CoreLib \
  -i managed.dll -c -o injected.dll add_c.s
```

これは、chibiccなどで生成されるC言語のソースコードを、直接C#ソースコードと合成して生成させ、
単一の .NET相互運用アセンブリファイルを生成出来る事を意味します。

現在のバージョンでは、CILソースコードから注入対象 .NETアセンブリ内のシンボルを参照することが出来ます。

* 将来のバージョンで、.NETアセンブリからCILの関数シンボルを参照する事が出来るようにする予定です。


----

## ソースコードのビルド

ビルドは.NET 8 SDK環境で可能です。ビルドに必要な前提条件はありません。
例えば:

```bash
$ dotnet build chibicc-cil-toolchain.sln
```

テストは現在のところ、ネイティブバイナリのILDAsmに依存しているため、Windows x64環境またはLinux x64でのみ実行できます。
手元の環境で、30秒ほどかかりました。

```bash
$ dotnet test chibicc-cil-toolchain.sln
```

`build-nupkg.bat`又は`build-nupkg.sh`を使用すると、NuGetパッケージを`artifacts`ディレクトリに生成します。
`chibias.net4`と`chibild.net4`プロジェクトは、`Release`ビルドで`net48`向けの単一ファイルバイナリを生成します。


----

## TODO

[TODO (英語)](https://github.com/kekyo/chibicc-cil-toolchain#todo) を参照してください。

----

## chibicc-toolchainの背景

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
