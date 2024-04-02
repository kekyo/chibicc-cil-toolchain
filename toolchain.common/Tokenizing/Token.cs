/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

namespace chibicc.toolchain.Tokenizing;

public sealed class Token
{
    public readonly TokenTypes Type;

    public readonly string Text;
    public readonly uint Line;
    public readonly uint StartColumn;
    public readonly uint EndColumn;

    public Token(
        TokenTypes type,
        string text,
        uint line,
        uint startColumn,
        uint endColumn)
    {
        this.Type = type;
        this.Text = text;
        this.Line = line;
        this.StartColumn = startColumn;
        this.EndColumn = endColumn;
    }

    public override string ToString() =>
        $"{this.Type}: {this.Text}";
}
