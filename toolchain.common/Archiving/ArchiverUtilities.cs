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
using System.IO.Compression;
using System.Linq;
using System.Text;
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Archiving;

public static class ArchiverUtilities
{
    public static readonly string SymbolTableFileName = "__symtable$";
    
    public static IEnumerable<Symbol> EnumerateSymbolsFromObjectFile(
        Stream objectFileStream, string pairedFileName)
    {
        var tr = new StreamReader(objectFileStream, Encoding.UTF8, true);

        foreach (var tokens in CilTokenizer.TokenizeAll("", SymbolTableFileName, tr))
        {
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
                                yield return new(directive, scope, tokens[3], pairedFileName);
                            }
                            break;
                        case "structure":
                            yield return new(directive, scope, tokens[2], pairedFileName);
                            break;
                    }
                }
            }
        }
    }
    
    public static void WriteSymbolTable(Stream symbolTableStream, Symbol[] symbols)
    {
        var tw = new StreamWriter(symbolTableStream, Encoding.UTF8);

        foreach (var symbol in symbols)
        {
            tw.WriteLine($".{symbol.Directive.Text} {symbol.Name.Text} {symbol.ArchiveItemName}");
        }

        tw.Flush();
    }
    
    public static IEnumerable<Symbol> EnumerateSymbolTable(string archiveFilePath)
    {
        using var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        if (archive.GetEntry(SymbolTableFileName) is { } entry)
        {
            using var stream = entry.Open();
            var tr = new StreamReader(stream, Encoding.UTF8, true);
        
            foreach (var tokens in CilTokenizer.TokenizeAll("", SymbolTableFileName, tr).
                Where(tokens => tokens.Length >= 3))
            {
                var directive = tokens[0];
                var name = tokens[1];
                var archiveItemName = tokens[2];

                if (directive.Type == TokenTypes.Directive &&
                    name.Type == TokenTypes.Identity &&
                    archiveItemName.Type == TokenTypes.Identity)
                {
                    yield return new(directive, null, name, archiveItemName.Text);
                }
            }
        }
    }

    public static IEnumerable<string> EnumerateArchiveItemNames(string archiveFilePath)
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

    public static Stream OpenArchiveItem(string archiveFilePath, string itemName)
    {
        var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        return new ArchiveItemStream(archive, archive.GetEntry(itemName)!.Open());
    }
}
