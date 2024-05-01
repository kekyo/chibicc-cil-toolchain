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
    private static SymbolList ReadSymbols(
        string objectFilePath,
        SymbolTableModes symbolTableMode)
    {
        var objectName = Path.GetFileNameWithoutExtension(objectFilePath) + ".o";
        
        if (symbolTableMode == SymbolTableModes.ForceUpdate ||
            (symbolTableMode == SymbolTableModes.Auto && Path.GetExtension(objectFilePath) is ".o" or ".s"))
        {
            using var ofs = StreamUtilities.OpenStream(objectFilePath, false);

            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFile(ofs).
                ToArray();

            return new SymbolList(objectName, symbols);
        }
        else
        {
            return new SymbolList(objectName, new Symbol[0]);
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

        var symbolLists = new SymbolList[objectFilePaths.Length];

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
                    var symbolList = ReadSymbols(objectFilePath, symbolTableMode);
                    symbolLists[index] = symbolList;
                }))).
            ToArray();
        
        Parallel.Invoke(tasks);

        if (archive != null)
        {
            var symbolTableEntry = archive.CreateEntry(
                ArchiverUtilities.SymbolTableFileName, CompressionLevel.NoCompression);
                            
            using var afs = symbolTableEntry.Open();

            ArchiverUtilities.WriteSymbolTable(afs, symbolLists);
        }

        return updated ? AddResults.Updated : AddResults.Created;
    }
}
