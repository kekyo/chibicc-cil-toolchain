/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using chibicc.toolchain.Archiving;
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
    private static Stream OpenStream(string path, bool writable) =>
        (path == "-") ?
            (writable ? Console.OpenStandardOutput() : Console.OpenStandardInput()) :
            new FileStream(
                path,
                writable ? FileMode.Create : FileMode.Open,
                writable ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read);

    private static Symbol[] ReadSymbols(string objectFilePath, SymbolTableModes symbolTableMode)
    {
        if (symbolTableMode == SymbolTableModes.ForceUpdate ||
            (symbolTableMode == SymbolTableModes.Auto && Path.GetExtension(objectFilePath) is ".o" or ".s"))
        {
            using var ofs = OpenStream(objectFilePath, false);

            var tr = new StreamReader(ofs, Encoding.UTF8, true);

            var fileName = Path.GetFileNameWithoutExtension(objectFilePath) + ".o";
            var symbols = ArchiverUtilities.EnumerateSymbols(tr, fileName).
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
                        archive?.GetEntry(ArchiverUtilities.SymbolTableFileName) is { } symbolTableEntry)
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
            var symbolTableEntry = archive.CreateEntry(
                ArchiverUtilities.SymbolTableFileName, CompressionLevel.Optimal);
                            
            using var afs = symbolTableEntry.Open();

            WriteSymbolTable(afs, symbols);
        }

        return updated ? AddResults.Updated : AddResults.Created;
    }
}
