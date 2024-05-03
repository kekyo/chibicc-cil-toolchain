/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using chibicc.toolchain.Internal;

namespace chibicc.toolchain.Archiving;

public static class ArchiverUtilities
{
    public static readonly string SymbolTableFileName = "__symtable$";
    
    public static IEnumerable<Symbol> EnumerateSymbolsFromObjectFile(
        Stream objectFileStream)
    {
        var tr = new StreamReader(objectFileStream, Encoding.UTF8, true);

        foreach (var tokens in CilTokenizer.TokenizeAll("", SymbolTableFileName, tr))
        {
            if (tokens.Length >= 3 &&
                tokens[0] is (TokenTypes.Directive, var directive) &&
                tokens[1] is (TokenTypes.Identity, var scope))
            {
                switch (directive)
                {
                    case "function":
                    case "global":
                    case "enumeration":
                        if (tokens.Length >= 4 &&
                            tokens[3] is (TokenTypes.Identity, var name1))
                        {
                            yield return new(directive, scope, name1);
                        }
                        break;
                    case "structure":
                        if (tokens[2] is (TokenTypes.Identity, var name2))
                        {
                            yield return new(directive, scope, name2);
                        }
                        break;
                }
            }
        }
    }
    
    public static void WriteSymbolTable(
        Stream symbolTableStream,
        SymbolList[] symbolLists)
    {
        var tw = new StreamWriter(symbolTableStream, Encoding.UTF8);

        foreach (var symbolList in symbolLists)
        {
            tw.WriteLine($".object {symbolList.ObjectName}");

            foreach (var symbol in symbolList.Symbols.Distinct())
            {
                tw.WriteLine($"{symbol.Directive} {symbol.Scope} {symbol.Name}");
            }
        }

        tw.Flush();
    }
    
    public static IEnumerable<SymbolList> EnumerateSymbolTable(
        string archiveFilePath)
    {
        using var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        if (archive.GetEntry(SymbolTableFileName) is { } entry)
        {
            using var stream = entry.Open();
            var tr = new StreamReader(stream, Encoding.UTF8, true);

            Token? currentObjectName = null;
            var symbols = new List<Symbol>();            
            
            foreach (var tokens in CilTokenizer.TokenizeAll("", SymbolTableFileName, tr).
                Where(tokens => tokens.Length >= 2))
            {
                switch (tokens[0])
                {
                    case (TokenTypes.Directive, "object")
                        when tokens[1] is (TokenTypes.Identity, _):
                        if (currentObjectName is (_, var objectName))
                        {
                            yield return new(objectName, symbols.ToArray());
                        }
                        currentObjectName = tokens[1];
                        symbols.Clear();
                        break;
                    case (TokenTypes.Identity, var directive)
                    when tokens.Length >= 3 &&
                         tokens[1] is (TokenTypes.Identity, var scope) &&
                         tokens[2] is (TokenTypes.Identity, var name):
                        symbols.Add(new(directive, scope, name));
                        break;
                }
            }

            if (currentObjectName is var (_, con2))
            {
                yield return new(con2, symbols.ToArray());
            }
        }
    }

    public static IEnumerable<string> EnumerateArchivedObjectNames(
        string archiveFilePath)
    {
        using var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        foreach (var entry in archive.Entries.
            Where(entry => entry.Name != SymbolTableFileName))
        {
            yield return entry.Name;
        }
    }

    public static Stream OpenArchivedObject(string archiveFilePath, string objectName)
    {
        var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        return new ArchiveObjectStream(archive, archive.GetEntry(objectName)!.Open());
    }
}
