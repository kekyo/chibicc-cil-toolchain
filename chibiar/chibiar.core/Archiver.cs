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
using chibiar.cli;
using chibicc.toolchain.Archiving;
using chibicc.toolchain.Internal;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;

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
    private readonly ILogger logger;

    public Archiver(ILogger logger) =>
        this.logger = logger;

    private SymbolList ReadSymbols(
        string objectFilePath,
        SymbolTableModes symbolTableMode)
    {
        var objectName = Path.GetFileNameWithoutExtension(objectFilePath) + ".o";
        
        if (symbolTableMode == SymbolTableModes.ForceUpdate ||
            (symbolTableMode == SymbolTableModes.Auto && Path.GetExtension(objectFilePath) is ".o" or ".s"))
        {
            using var ofs = ObjectStreamUtilities.OpenObjectStream(objectFilePath, false);

            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFile(ofs).
                ToArray();

            return new SymbolList(objectName, symbols);
        }
        else
        {
            return new SymbolList(objectName, CommonUtilities.Empty<Symbol>());
        }
    }

    private static bool IsSourceFile(string path) =>
        Path.GetExtension(path) is not ".o";

    private static Stream OpenObjectStreamToCompressed(string objectFilePath)
    {
        var ofs = StreamUtilities.OpenStream(objectFilePath, false);
    
        return IsSourceFile(objectFilePath) ?
            new GZipStream(ofs, CompressionLevel.Optimal) : ofs;
    }

    internal AddResults Run(
        string archiveFilePath,
        SymbolTableModes symbolTableMode,
        string[] objectFilePaths,
        bool isUpdateMode,
        bool isDryrun)
    {
        var isUpdateArchive = File.Exists(archiveFilePath);
        
        using var archive = isDryrun ?
            null : ZipFile.Open(
                archiveFilePath,
                isUpdateArchive ? ZipArchiveMode.Update : ZipArchiveMode.Create,
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
                            using var ofs = OpenObjectStreamToCompressed(objectFilePath);

                            var fileName = IsSourceFile(objectFilePath) ?
                                (Path.GetFileNameWithoutExtension(objectFilePath) + ".o") :
                                Path.GetFileName(objectFilePath);
                            var dateTime = File.GetLastWriteTime(objectFilePath);

                            if (isUpdateMode &&
                                archive.Entries.FirstOrDefault(e => e.FullName == fileName) is { } entry)
                            {
                                if (entry.LastWriteTime < dateTime)
                                {
                                    entry.Delete();
                                    
                                    entry = archive.CreateEntry(
                                        fileName,
                                        CompressionLevel.NoCompression);  // ofs is already gzip compressed.

                                    entry.LastWriteTime = dateTime;
                        
                                    using var afs = entry.Open();
                                    ofs.CopyTo(afs);
                        
                                    afs.Flush();
                                }
                            }
                            else
                            {
                                entry = archive.CreateEntry(
                                    fileName,
                                    CompressionLevel.NoCompression);  // ofs is already gzip compressed.

                                entry.LastWriteTime = dateTime;
                        
                                using var afs = entry.Open();
                                ofs.CopyTo(afs);
                        
                                afs.Flush();
                            }
                        }
                    }

                    if (isUpdateArchive &&
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
                    var symbolList = this.ReadSymbols(objectFilePath, symbolTableMode);
                    symbolLists[index] = symbolList;
                }))).
            ToArray();
        
        Parallel.Invoke(tasks);

        if (archive != null)
        {
            var symbolTableEntry = archive.CreateEntry(
                ArchiverUtilities.SymbolTableFileName, CompressionLevel.Optimal);
                            
            using var afs = symbolTableEntry.Open();

            ArchiverUtilities.WriteSymbolTable(afs, symbolLists);
        }

        return isUpdateArchive ? AddResults.Updated : AddResults.Created;
    }

    public void Archive(CliOptions options)
    {
        switch (options.Mode)
        {
            case ArchiveModes.Add:
                if (this.Run(
                    options.ArchiveFilePath,
                    options.SymbolTableMode,
                    options.ObjectFilePaths.ToArray(),
                    false,
                    options.IsDryRun) == AddResults.Created &&
                    !options.IsSilent)
                {
                    this.logger.Information($"creating {Path.GetFileName(options.ArchiveFilePath)}");
                }
                break;
            case ArchiveModes.Update:
                if (this.Run(
                    options.ArchiveFilePath,
                    options.SymbolTableMode,
                    options.ObjectFilePaths.ToArray(),
                    true,
                    options.IsDryRun) == AddResults.Created &&
                    !options.IsSilent)
                {
                    this.logger.Information($"creating {Path.GetFileName(options.ArchiveFilePath)}");
                }
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
