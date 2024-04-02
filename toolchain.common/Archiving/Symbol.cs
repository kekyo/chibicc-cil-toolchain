/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Archiving;

public readonly struct Symbol
{
    public readonly Token Directive;
    public readonly Token Scope;
    public readonly Token Name;
    public readonly string FileName;

    public Symbol(Token directive, Token scope, Token name, string fileName)
    {
        this.Directive = directive;
        this.Scope = scope;
        this.Name = name;
        this.FileName = fileName;
    }
}
