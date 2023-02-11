/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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

internal sealed class Tokenizer
{
    private enum EscapeStates
    {
        NonEscape,
        First,
        Byte3,
        Byte2,
        Byte1,
        Byte0,
    }

    private uint lineIndex;

    public Token[] TokenizeLine(string line)
    {
        var tokens = new List<Token>();
        var hex = new StringBuilder();
        var sb = new StringBuilder();

        for (var index = 0; index < line.Length; index++)
        {
            var inch = line[index];
            if (!char.IsWhiteSpace(inch))
            {
                if (inch == ';')
                {
                    break;
                }
                else if (inch == '.' && tokens.Count == 0)
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
                        (uint)start,
                        (uint)index));
                }
                else if (inch == '"')
                {
                    index++;
                    var start = index;
                    var escapeState = EscapeStates.NonEscape;
                    while (index < line.Length)
                    {
                        inch = line[index];
                        if (escapeState == EscapeStates.NonEscape)
                        {
                            if (inch == '\\')
                            {
                                escapeState = EscapeStates.First;
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
                        else if (escapeState == EscapeStates.First)
                        {
                            switch (inch)
                            {
                                case 'a':
                                    sb.Append('\a');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 'b':
                                    sb.Append('\b');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 'f':
                                    sb.Append('\f');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 'n':
                                    sb.Append('\n');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 'r':
                                    sb.Append('\r');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 't':
                                    sb.Append('\t');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 'u':
                                    escapeState = EscapeStates.Byte3;
                                    break;
                                case 'v':
                                    sb.Append('\v');
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                                case 'x':
                                    escapeState = EscapeStates.Byte1;
                                    break;
                                default:
                                    sb.Append(inch);
                                    escapeState = EscapeStates.NonEscape;
                                    break;
                            }
                        }
                        else if (escapeState == EscapeStates.Byte3)
                        {
                            hex.Append(inch);
                            escapeState = EscapeStates.Byte2;
                        }
                        else if (escapeState == EscapeStates.Byte2)
                        {
                            hex.Append(inch);
                            escapeState = EscapeStates.Byte1;
                        }
                        else if (escapeState == EscapeStates.Byte1)
                        {
                            hex.Append(inch);
                            escapeState = EscapeStates.Byte0;
                        }
                        else if (escapeState == EscapeStates.Byte0)
                        {
                            hex.Append(inch);
                            if (ushort.TryParse(
                                hex.ToString(),
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out var rawValue))
                            {
                                sb.Append((char)rawValue);
                            }
                            hex.Clear();
                            escapeState = EscapeStates.NonEscape;
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                        index++;
                    }
                    tokens.Add(new(
                        TokenTypes.String,
                        sb.ToString(),
                        this.lineIndex,
                        (uint)start,
                        (uint)index));
                    hex.Clear();
                    sb.Clear();
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
                            (uint)start,
                            (uint)index - 1));
                    }
                    else
                    {
                        tokens.Add(new(
                            TokenTypes.Identity,
                            line.Substring(start, index - start),
                            this.lineIndex,
                            (uint)start,
                            (uint)index));
                    }
                }
            }
        }

        this.lineIndex++;

        return tokens.ToArray();
    }
}
