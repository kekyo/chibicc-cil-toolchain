/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Archiving;

public static class ArchiverUtilities
{
    public static readonly string SymbolTableFileName = "__symtable$";
    
    public static IEnumerable<Symbol> EnumerateSymbols(TextReader tr, string fileName)
    {
        var tokenizer = new Tokenizer();

        while (true)
        {
            var line = tr.ReadLine();
            if (line == null)
            {
                break;
            }

            var tokens = tokenizer.TokenizeLine(line);
            if (tokens.Length >= 3)
            {
                var directive = tokens[0];
                var scope = tokens[1];
                if (directive.Type == TokenTypes.Directive &&
                    scope.Type == TokenTypes.Identity &&
                    scope.Text is "public" or "internal")
                {
                    switch (directive.Text)
                    {
                        case "function":
                        case "global":
                        case "enumeration":
                            if (tokens.Length >= 4)
                            {
                                yield return new(directive, scope, tokens[3], fileName);
                            }
                            break;
                        case "structure":
                            yield return new(directive, scope, tokens[2], fileName);
                            break;
                    }
                }
            }
        }
    }
    
    public static void WriteSymbolTable(Stream stream, Symbol[] symbols)
    {
        var tw = new StreamWriter(stream, Encoding.UTF8);

        foreach (var symbol in symbols)
        {
            tw.WriteLine($".{symbol.Directive.Text} {symbol.Name.Text} {symbol.FileName}");
        }

        tw.Flush();
    }
}
