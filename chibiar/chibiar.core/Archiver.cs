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
using chibicc.toolchain.IO;

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
    private static Symbol[] ReadSymbols(string objectFilePath, SymbolTableModes symbolTableMode)
    {
        if (symbolTableMode == SymbolTableModes.ForceUpdate ||
            (symbolTableMode == SymbolTableModes.Auto && Path.GetExtension(objectFilePath) is ".o" or ".s"))
        {
            using var ofs = StreamUtilities.OpenStream(objectFilePath, false);

            var fileName = Path.GetFileNameWithoutExtension(objectFilePath) + ".o";
            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFile(ofs, fileName).
                Distinct(SymbolComparer.Instance).
                OrderBy(symbol => symbol.Directive.Text).
                ThenBy(symbol => symbol.Name.Text).
                ToArray();

            return symbols;
        }
        else
        {
            return new Symbol[0];
        }
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
                            using var ofs = StreamUtilities.OpenStream(objectFilePath, false);
                            
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
            OrderBy(symbol => symbol.Directive.Text).
            ThenBy(symbol => symbol.Name.Text).
            ToArray();

        if (archive != null)
        {
            var symbolTableEntry = archive.CreateEntry(
                ArchiverUtilities.SymbolTableFileName, CompressionLevel.Optimal);
                            
            using var afs = symbolTableEntry.Open();

            ArchiverUtilities.WriteSymbolTable(afs, symbols);
        }

        return updated ? AddResults.Updated : AddResults.Created;
    }
}
