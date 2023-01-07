/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Text;

namespace chibias.Internal;

internal enum TokenTypes
{
    Directive,
    Label,
    String,
    Identity,
}

internal sealed class Token
{
    public readonly TokenTypes Type;

    public readonly string Text;
    public readonly int Line;
    public readonly int StartColumn;
    public readonly int EndColumn;

    public Token(
        TokenTypes type,
        string text,
        int line,
        int startColumn,
        int endColumn)
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

internal sealed class Tokenizer
{
    private int lineIndex;

    public Token[] TokenizeLine(
        string line)
    {
        var tokens = new List<Token>();

        for (var index = 0; index < line.Length; index++)
        {
            var inch = line[index];
            if (!char.IsWhiteSpace(inch))
            {
                if (inch == ';')
                {
                    break;
                }
                else if (inch == '.')
                {
                    index++;
                    var start = index;
                    while (index < line.Length)
                    {
                        inch = line[index];
                        if (char.IsWhiteSpace(inch) || inch == ';')
                        {
                            break;
                        }
                        index++;
                    }
                    tokens.Add(new(
                        TokenTypes.Directive,
                        line.Substring(start, index - start),
                        this.lineIndex,
                        start,
                        index - 1));
                }
                else if (inch == '"')
                {
                    index++;
                    var start = index;
                    var escaped = false;
                    var sb = new StringBuilder();
                    while (index < line.Length)
                    {
                        inch = line[index];
                        if (!escaped)
                        {
                            if (inch == '\\')
                            {
                                escaped = true;
                            }
                            else if (inch == '"')
                            {
                                break;
                            }
                            else
                            {
                                sb.Append(inch);
                            }
                        }
                        else
                        {
                            sb.Append(inch);
                            escaped = false;
                        }
                        index++;
                    }
                    tokens.Add(new(
                        TokenTypes.String,
                        sb.ToString(),
                        this.lineIndex,
                        start,
                        index - 1));
                }
                else
                {
                    var start = index;
                    index++;
                    while (index < line.Length)
                    {
                        inch = line[index];
                        if (char.IsWhiteSpace(inch) || inch == ';')
                        {
                            break;
                        }
                        index++;
                    }

                    if (line[index - 1] == ':')
                    {
                        tokens.Add(new(
                            TokenTypes.Label,
                            line.Substring(start, index - start - 1),
                            this.lineIndex,
                            start,
                            index - 1 - 1));
                    }
                    else
                    {
                        tokens.Add(new(
                            TokenTypes.Identity,
                            line.Substring(start, index - start),
                            this.lineIndex,
                            start,
                            index - 1));
                    }
                }
            }
        }

        this.lineIndex++;

        return tokens.ToArray();
    }
}
