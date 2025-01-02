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
using chibiar.Cli;
using chibicc.toolchain.Archiving;
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

    private static string GetObjectName(string objectFilePath) =>
        IsSourceFile(objectFilePath) ?
            (Path.GetFileNameWithoutExtension(objectFilePath) + ".o") :
            Path.GetFileName(objectFilePath);

    private static void WriteSymbolTable(
        Stream symbolTableStream,
        SymbolList symbolList)
    {
        var tw = StreamUtilities.CreateTextWriter(symbolTableStream);

        tw.WriteLine($".object {symbolList.ObjectName}");

        foreach (var symbol in symbolList.Symbols.Distinct())
        {
            tw.WriteLine($"    {symbol.Directive} {symbol.Scope} {symbol.Name}{(symbol.MemberCount is { } mc ? $" {mc}" : "")}");
        }

        tw.Flush();
    }

    internal bool AddOrUpdate(
        string archiveFilePath,
        string[] objectFilePaths,
        bool isCreateSymbolTable,
        bool isDryrun)
    {
        var outputArchiveFilePath = $"{archiveFilePath}_{Guid.NewGuid():N}";

        try
        {
            using var archiveStream = isDryrun ?
                new NullStream() :
                StreamUtilities.OpenStream(outputArchiveFilePath, true);

            var muxer = new StreamMuxer(archiveStream);

            using var symbolStream = isCreateSymbolTable ?
                muxer.CreateSubStream(ArchiverUtilities.SymbolTableFileName) : null;

            foreach (var objectFilePath in objectFilePaths)
            {
                using var objectFileStream = StreamUtilities.OpenStream(
                    objectFilePath, false);
                var objectName = GetObjectName(objectFilePath);

                if (symbolStream != null)
                {
                    var objectDecompressedStream = new GZipStream(
                        objectFileStream, CompressionMode.Decompress);
                    var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFile(objectDecompressedStream).
                        ToArray();
                    var symbolList = new SymbolList(objectName, symbols);
                    WriteSymbolTable(symbolStream, symbolList);

                    objectFileStream.Position = 0;
                }

                using var objectToStream = muxer.CreateSubStream(objectName);
                objectFileStream.CopyTo(objectToStream);
                objectFileStream.Flush();
            }

            archiveStream.Flush();
        }
        catch
        {
            File.Delete(outputArchiveFilePath);
            throw;
        }

        if (!isDryrun)
        {
            if (File.Exists(archiveFilePath))
            {
                try
                {
                    File.Delete(archiveFilePath);
                }
                catch
                {
                }
            }
            File.Move(outputArchiveFilePath, archiveFilePath);
        }

        return true;
    }

    internal void Extract(
        string archiveFilePath,
        string[] objectNames,
        bool isDryrun)
    {
        using var stream = StreamUtilities.OpenStream(archiveFilePath, false);

        var demuxer = new StreamDemuxer(stream);
        var ids = new Dictionary<int, Stream>();
        var ons = new HashSet<string>(objectNames);

        while (true)
        {
            if (demuxer.Prepare() is not { } entry)
            {
                break;
            }
            Stream? objectStream;
            if (entry.Name is { } name && ons.Contains(name))
            {
                if (!ids.TryGetValue(entry.Id, out objectStream))
                {
                    objectStream = isDryrun ?
                        new NullStream() :
                        StreamUtilities.OpenStream(entry.Name, true);
                    ids.Add(entry.Id, objectStream);
                }
            }
            else
            {
                if (!ids.TryGetValue(entry.Id, out objectStream))
                {
                    this.logger.Error($"Object is not found: {entry.Id}");
                }
            }
            if (entry.Length >= 1)
            {
                if (objectStream != null)
                {
                    var buffer = demuxer.Read(entry);
                    objectStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    demuxer.Skip(entry);
                }
            }
            else
            {
                if (objectStream != null)
                {
                    objectStream.Flush();
                    objectStream.Close();
                }
                demuxer.Finish(entry);
                ids.Remove(entry.Id);
            }
        }
    }

    internal void List(
        string archiveFilePath,
        string[] objectNames)
    {
        if (objectNames.Length >= 1)
        {
            var existObjectNames = new HashSet<string>(
                ArchiverUtilities.EnumerateArchivedObjectNames(archiveFilePath));

            foreach (var objectName in objectNames.
                Where(existObjectNames.Contains))
            {
                Console.WriteLine(objectName);
            }
        }
        else
        {
            foreach (var objectName in
                ArchiverUtilities.EnumerateArchivedObjectNames(archiveFilePath))
            {
                Console.WriteLine(objectName);
            }
        }
    }

    public void Archive(CliOptions options)
    {
        switch (options.Mode)
        {
            case ArchiveModes.AddOrUpdate:
                if (this.AddOrUpdate(
                    options.ArchiveFilePath,
                    options.ObjectNames.ToArray(),
                    options.IsCreateSymbolTable,
                    options.IsDryRun) &&
                    !options.IsSilent)
                {
                    this.logger.Information(
                        $"creating {Path.GetFileName(options.ArchiveFilePath)}");
                }
                break;
            case ArchiveModes.Extract:
                this.Extract(
                    options.ArchiveFilePath,
                    options.ObjectNames.ToArray(),
                    options.IsDryRun);
                break;
            case ArchiveModes.List:
                this.List(
                    options.ArchiveFilePath,
                    options.ObjectNames.ToArray());
                break;
            default:
                throw new NotImplementedException();
        }
    }
}
