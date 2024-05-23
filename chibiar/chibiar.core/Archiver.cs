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
using chibiar.Cli;
using chibicc.toolchain.Archiving;
using chibicc.toolchain.Internal;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;

namespace chibiar;

public sealed class Archiver
{
    private readonly ILogger logger;

    public Archiver(ILogger logger) =>
        this.logger = logger;

    private static bool IsSourceFile(string path) =>
        Path.GetExtension(path) is not ".o";

    private static Stream OpenObjectStreamToCompressed(string objectFilePath)
    {
        var ofs = StreamUtilities.OpenStream(objectFilePath, false);
    
        return IsSourceFile(objectFilePath) ?
            new GZipStream(ofs, CompressionLevel.Optimal) : ofs;
    }

    private static string GetObjectName(string objectFilePath) =>
        IsSourceFile(objectFilePath) ?
            (Path.GetFileNameWithoutExtension(objectFilePath) + ".o") :
            Path.GetFileName(objectFilePath);
        
    private static string[] AddOrUpdateObjectFiles(
        string archiveFilePath,
        bool isCreatedArchive,
        string[] objectFilePaths,
        bool isDryrun)
    {
        if (!isDryrun)
        {
            if (isCreatedArchive)
            {
                using var a = ZipFile.Open(
                    archiveFilePath,
                    ZipArchiveMode.Create,
                    Encoding.UTF8);
            }
            
            using var archive = ZipFile.Open(
                archiveFilePath,
                ZipArchiveMode.Update,
                Encoding.UTF8);
        
            if (!isCreatedArchive &&
                archive.Entries.FirstOrDefault(entry =>
                    entry.Name == ArchiverUtilities.SymbolTableFileName) is { } symbolTableEntry)
            {
                symbolTableEntry.Delete();
            }

            var entries = archive.Entries.
                ToDictionary(entry => entry.Name);

            foreach (var objectFilePath in objectFilePaths)
            {
                var objectName = GetObjectName(objectFilePath);
                if (entries.TryGetValue(objectName, out var entry))
                {
                    var lastWriteTime = File.GetLastWriteTime(objectFilePath);
                    if (lastWriteTime <= entry.LastWriteTime)
                    {
                        continue;
                    }
                    entry.Delete();
                }

                entry = archive.CreateEntry(
                    objectName,
                    CompressionLevel.NoCompression);

                using var outputStream = entry.Open();
                using var inputStream = OpenObjectStreamToCompressed(objectFilePath);
        
                inputStream.CopyTo(outputStream);
                outputStream.Flush();
            }
            
            return archive.Entries.
                Select(entry => entry.Name).
                ToArray();
        }
        else if (!isCreatedArchive)
        {
            using var archive = ZipFile.Open(
                archiveFilePath,
                ZipArchiveMode.Read,
                Encoding.UTF8);
        
            var entries = archive.Entries.
                Select(entry => entry.Name).
                ToList();
            entries.Remove(ArchiverUtilities.SymbolTableFileName);

            foreach (var objectFilePath in objectFilePaths)
            {
                var objectName = GetObjectName(objectFilePath);
                if (!entries.Contains(objectName))
                {
                    entries.Add(objectName);
                }
            }

            return entries.ToArray();
        }
        else
        {
            return objectFilePaths.
                Select(GetObjectName).
                ToArray();
        }
    }

    private static SymbolList[] GetSymbolLists(
        string archiveFilePath,
        string[] objectNames,
        bool isDryrun)
    {
        var symbolLists = new SymbolList[objectNames.Length]!;

        if (!isDryrun || File.Exists(archiveFilePath))
        {
            Parallel.ForEach(objectNames,
                (objectName, _, index) =>
                {
                    using var stream = ArchiverUtilities.OpenArchivedObject(
                        archiveFilePath,
                        objectName);
                
                    var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFile(stream).
                        ToArray();

                    symbolLists[index] = new(objectName, symbols);
                });
        }
        else
        {
            for (var index = 0; index < symbolLists.Length; index++)
            {
                symbolLists[index] = new(objectNames[index], CommonUtilities.Empty<Symbol>());
            }
        }

        return symbolLists;
    }

    private static void AddSymbolTable(
        string archiveFilePath,
        SymbolList[] symbolLists,
        bool isDryrun)
    {
        if (!isDryrun)
        {
            using var archive = ZipFile.Open(
                archiveFilePath,
                ZipArchiveMode.Update,
                Encoding.UTF8);

            var symbolTableEntry = archive.CreateEntry(
                ArchiverUtilities.SymbolTableFileName,
                CompressionLevel.Optimal);
        
            using var outputStream = symbolTableEntry.Open();

            ArchiverUtilities.WriteSymbolTable(outputStream, symbolLists);
        }
        else
        {
            ArchiverUtilities.WriteSymbolTable(new MemoryStream(), symbolLists);
        }
    }

    internal bool AddOrUpdate(
        string archiveFilePath,
        string[] objectFilePaths,
        bool isDryrun)
    {
        var isCreatedArchive = !File.Exists(archiveFilePath);

        var objectNames = AddOrUpdateObjectFiles(
            archiveFilePath,
            isCreatedArchive,
            objectFilePaths,
            isDryrun);

        var symbolLists = GetSymbolLists(
            archiveFilePath,
            objectNames,
            isDryrun);

        AddSymbolTable(
            archiveFilePath,
            symbolLists,
            isDryrun);

        return isCreatedArchive;
    }

    public void Archive(CliOptions options)
    {
        switch (options.Mode)
        {
            case ArchiveModes.AddOrUpdate:
                if (this.AddOrUpdate(
                    options.ArchiveFilePath,
                    options.ObjectFilePaths.ToArray(),
                    options.IsDryRun) &&
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
