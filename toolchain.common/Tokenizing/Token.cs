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

    public readonly string? BasePath;
    public readonly string RelativePath;
    public readonly uint Line;
    public readonly uint StartColumn;
    public readonly uint EndColumn;

    public Token(
        TokenTypes type,
        string text,
        string? basePath,
        string relativePath,
        uint line,
        uint startColumn,
        uint endColumn)
    {
        this.Type = type;
        this.Text = text;
        this.BasePath = basePath;
        this.RelativePath = relativePath;
        this.Line = line;
        this.StartColumn = startColumn;
        this.EndColumn = endColumn;
    }

    private string DebuggerString =>
        $"{this.Type}: {this.Text}";

    public override string ToString() =>
        this.Type switch
        {
            TokenTypes.Directive => $".{this.Text}",
            TokenTypes.Label => $"{this.Text}:",
            TokenTypes.String => $"\"{this.Text}\"",
            _ => this.Text,
        };

    public void Deconstruct(
        out TokenTypes type,
        out string text)
    {
        type = this.Type;
        text = this.Text;
    }
}
