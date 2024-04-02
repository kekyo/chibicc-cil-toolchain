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
using System.Threading.Tasks;
using chibild.Tokenizing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace chibiar;

public enum SymbolTableModes
{
    Auto,
    ForceUpdate,
    ForceIgnore,
}

public enum AddResults
{
    Created,
    Updated,
}

public sealed class Archiver
{
    public static readonly string SymbolTableFileName = "__symtable$";
    
    private static Stream OpenStream(string path, bool writable) =>
        (path == "-") ?
            (writable ? Console.OpenStandardOutput() : Console.OpenStandardInput()) :
            new FileStream(
                path,
                writable ? FileMode.Create : FileMode.Open,
                writable ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read);

    private readonly struct Symbol
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

    private sealed class SymbolComparer : IEqualityComparer<Symbol>
    {
        public bool Equals(Symbol x, Symbol y) =>
            x.Directive.Text.Equals(y.Directive.Text) &&
            x.Name.Text.Equals(y.Name.Text);

        public int GetHashCode(Symbol obj)
        {
            unchecked
            {
                return
                    (obj.Directive.Text.GetHashCode() * 397) ^
                     obj.Name.Text.GetHashCode();
            }
        }

        public static readonly SymbolComparer Instance = new();
    }

    private static IEnumerable<Symbol> EnumerateSymbols(TextReader tr, string fileName)
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

    private static Symbol[] ReadSymbols(string objectFilePath, SymbolTableModes symbolTableMode)
    {
        if (symbolTableMode == SymbolTableModes.ForceUpdate ||
            (symbolTableMode == SymbolTableModes.Auto && Path.GetExtension(objectFilePath) is ".o" or ".s"))
        {
            using var ofs = OpenStream(objectFilePath, false);

            var tr = new StreamReader(ofs, Encoding.UTF8, true);

            var fileName = Path.GetFileNameWithoutExtension(objectFilePath) + ".o";
            var symbols = EnumerateSymbols(tr, fileName).
                Distinct(SymbolComparer.Instance).
                ToArray();

            return symbols;
        }
        else
        {
            return new Symbol[0];
        }
    }

    private static void WriteSymbolTable(Stream stream, Symbol[] symbols)
    {
        var symbolsByDirectiveGroup = symbols.
            GroupBy(symbol => symbol.Directive.Text).
            ToDictionary(g => g.Key, g => g.ToArray());

        var symbolsByDirective = new JObject(
            symbolsByDirectiveGroup.
                Select(g => new JProperty(
                    g.Key,
                    new JObject(g.Value.Select(symbol => new JProperty(symbol.Name.Text, symbol.FileName)).ToArray<object>()))).
                ToArray<object>());

        var tw = new StreamWriter(stream, Encoding.UTF8);
        var jw = new JsonTextWriter(tw);
        jw.Formatting = Formatting.Indented;

        symbolsByDirective.WriteTo(jw);

        jw.Flush();
    }
    
    public AddResults Add(
        string archiveFilePath,
        SymbolTableModes symbolTableMode,
        string[] objectFilePaths,
        bool isDryrun)
    {
        var updated = File.Exists(archiveFilePath);
        
        using var archive = isDryrun ?
            null : ZipFile.Open(
                archiveFilePath,
                updated ? ZipArchiveMode.Update : ZipArchiveMode.Create,
                Encoding.UTF8);

        var symbolLists = new Symbol[objectFilePaths.Length][];

        var tasks = new[]
            {
                () =>
                {
                    foreach (var objectFilePath in objectFilePaths)
                    {
                        if (archive != null)
                        {
                            using var ofs = OpenStream(objectFilePath, false);
                            
                            var fileName = Path.GetExtension(objectFilePath) == ".s" ?
                                (Path.GetFileNameWithoutExtension(objectFilePath) + ".o") :
                                Path.GetFileName(objectFilePath);
                            var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                            entry.LastWriteTime = File.GetLastWriteTime(objectFilePath);
                            
                            using var afs = entry.Open();
                            ofs.CopyTo(afs);
                            
                            afs.Flush();
                        }
                    }

                    if (updated &&
                        symbolTableMode != SymbolTableModes.ForceIgnore &&
                        archive?.GetEntry(SymbolTableFileName) is { } symbolTableEntry)
                    {
                        symbolTableEntry.Delete();
                    }
                },
            }.
            Concat(objectFilePaths.Select((objectFilePath, index) =>
                new Action(() =>
                {
                    var symbols = ReadSymbols(objectFilePath, symbolTableMode);
                    symbolLists[index] = symbols;
                }))).
            ToArray();
        
        Parallel.Invoke(tasks);
        
        var symbols = symbolLists.
            SelectMany(symbols => symbols).
            Distinct(SymbolComparer.Instance).
            ToArray();

        if (archive != null)
        {
            var symbolTableEntry = archive.CreateEntry(SymbolTableFileName, CompressionLevel.Optimal);
                            
            using var afs = symbolTableEntry.Open();

            WriteSymbolTable(afs, symbols);
        }

        return updated ? AddResults.Updated : AddResults.Created;
    }
}
