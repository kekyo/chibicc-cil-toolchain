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

        if (IsSourceFile(objectFilePath))
        {
            var gzs = new GZipStream(ofs, CompressionLevel.Optimal);
            var ms = new MemoryStream();
            gzs.CopyTo(ms);
            ofs.Close();
            ms.Position = 0;
            return ms;
        }
        else
        {
            return ofs;
        }
    }

    private static string GetObjectName(string objectFilePath) =>
        IsSourceFile(objectFilePath) ?
            (Path.GetFileNameWithoutExtension(objectFilePath) + ".o") :
            Path.GetFileName(objectFilePath);

    private interface IObjectItemDescriptor
    {
        int Length { get; }
        string ObjectName { get; }
    }

    private sealed class ArchivedObjectItemDescriptor : IObjectItemDescriptor
    {
        public long Position { get; private set; }
        public int Length { get; }
        public string ObjectName { get; }

        public ArchivedObjectItemDescriptor(long relativePosition, int length, string objectName)
        {
            this.Position = relativePosition;
            this.Length = length;
            this.ObjectName = objectName;
        }
        
        public void AdjustPosition(long basePosition) =>
            this.Position += basePosition;
    }

    private static IEnumerable<ArchivedObjectItemDescriptor> EnumerateArchivedObjectDescriptors(
        Stream archiveFileStream)
    {
        var ler = new LittleEndianReader(archiveFileStream);
        var relativePosition = 0;
        while (true)
        {
            if (!ler.TryReadString(256, out var objectName))
            {
                throw new FormatException(
                    $"Invalid object naming: Position={ler.Position?.ToString() ?? "(Unknown)"}");
            }
            if (objectName.Length == 0)
            {
                break;
            }
            if (!ler.TryReadInt32(out var length))
            {
                throw new FormatException(
                    $"Invalid object length: Position={ler.Position?.ToString() ?? "(Unknown)"}");
            }
            yield return new(relativePosition, length, objectName);
            relativePosition += length;
        }
    }

    private sealed class ObjectFileDescriptor : IObjectItemDescriptor
    {
        public int Length { get; }
        public string Path { get; }
        public string ObjectName =>
            GetObjectName(this.Path);

        public ObjectFileDescriptor(int length, string path)
        {
            this.Length = length;
            this.Path = path;
        }
    }

    private record struct SymbolListEntry(
        IObjectItemDescriptor Descriptor,
        SymbolList SymbolList);

    private static SymbolListEntry[] GetSymbolListEntries(
        string archiveFilePath,
        string[] objectFilePaths)
    {
        var archivedObjectItems = CommonUtilities.Empty<ArchivedObjectItemDescriptor>();
        var archiveFileBasePosition = 0L;

        if (File.Exists(archiveFilePath))
        {
            using var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false);
            var hashedObjectNames = new HashSet<string>(objectFilePaths.Select(GetObjectName));
            archivedObjectItems = EnumerateArchivedObjectDescriptors(archiveFileStream).
                Where(aod =>
                    !hashedObjectNames.Contains(aod.ObjectName) &&
                    aod.ObjectName != ArchiverUtilities.SymbolTableFileName).
                ToArray();
            archiveFileBasePosition = archiveFileStream.Position;
        }

        var symbolListEntries = new SymbolListEntry[objectFilePaths.Length + archivedObjectItems.Length];

        Parallel.ForEach(objectFilePaths.Concat(archivedObjectItems.Cast<object>()),
            (entry, _, index) =>
            {
                switch (entry)
                {
                    case string objectFilePath:
                        using (var objectStream = OpenObjectStreamToCompressed(objectFilePath))
                        {
                            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFileStream(objectStream).
                                ToArray();
                            var ofd = new ObjectFileDescriptor((int)objectStream.Length, objectFilePath);
                            symbolListEntries[index] = new(ofd, new(GetObjectName(objectFilePath), symbols));
                        }
                        break;
                    case ArchivedObjectItemDescriptor aod:
                        aod.AdjustPosition(archiveFileBasePosition);
                        using (var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false))
                        {
                            archiveFileStream.Position = aod.Position;
                            var objectStream = new RangedStream(archiveFileStream, aod.Length);
                            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFileStream(objectStream).
                                ToArray();
                            symbolListEntries[index] = new(aod, new(aod.ObjectName, symbols));
                        }
                        break;
                }
            });

        return symbolListEntries;
    }

    private void WriteObjectItemDescriptors(
        Stream outputArchiveFileStream,
        IEnumerable<IObjectItemDescriptor> descriptors)
    {
        var lew = new LittleEndianWriter(outputArchiveFileStream);
        foreach (var descriptor in descriptors)
        {
            lew.Write(descriptor.ObjectName);
            lew.Write(descriptor.Length);
        }
    }

    private static void WriteObjectItems(
        Stream outputArchiveFileStream,
        string archiveFilePath,
        IEnumerable<IObjectItemDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            switch (descriptor)
            {
                case ObjectFileDescriptor ofd:
                    using (var objectFileStream = OpenObjectStreamToCompressed(ofd.Path))
                    {
                        objectFileStream.CopyTo(outputArchiveFileStream);
                    }
                    break;
                case ArchivedObjectItemDescriptor aod:
                    using (var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false))
                    {
                        archiveFileStream.Position = aod.Position;
                        var objectStream = new RangedStream(archiveFileStream, aod.Length);
                        objectStream.CopyTo(outputArchiveFileStream);
                    }
                    break;
            }
        }
    }

    private sealed class SymbolTableDescriptor : IObjectItemDescriptor
    {
        public int Length { get; }
        public string ObjectName =>
            ArchiverUtilities.SymbolTableFileName;
        
        public SymbolTableDescriptor(int length) =>
            this.Length = length;
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
            var symbolListEntries = GetSymbolListEntries(
                outputArchiveFilePath,
                objectFilePaths);

            using (var outputArchiveFileStream = isDryrun ?
                new NullStream() :
                StreamUtilities.OpenStream(outputArchiveFilePath, true))
            {
                if (isCreateSymbolTable)
                {
                    var symbolTableStream = new MemoryStream();
                    ArchiverUtilities.WriteSymbolTable(
                        symbolTableStream,
                        symbolListEntries.Select(entry => entry.SymbolList));
                    symbolTableStream.Position = 0;

                    WriteObjectItemDescriptors(
                        outputArchiveFileStream,
                        symbolListEntries.
                            Select(entry => entry.Descriptor).
                            Prepend(new SymbolTableDescriptor((int)symbolTableStream.Length)));

                    symbolTableStream.CopyTo(outputArchiveFileStream);
                }
                else
                {
                    WriteObjectItemDescriptors(
                        outputArchiveFileStream,
                        symbolListEntries.Select(entry => entry.Descriptor));
                }

                WriteObjectItems(
                    outputArchiveFileStream,
                    archiveFilePath,
                    symbolListEntries.Select(entry => entry.Descriptor));

                outputArchiveFileStream.Flush();
            }
        
            File.Delete(archiveFilePath);
            File.Move(outputArchiveFilePath, archiveFilePath);
        }
        catch
        {
            File.Delete(outputArchiveFilePath);
            throw;
        }

        return false;
    }

    internal void Extract(
        string archiveFilePath,
        string[] objectNames,
        bool isDryrun)
    {
        var archivedObjectItems = CommonUtilities.Empty<ArchivedObjectItemDescriptor>();
        var archiveFileBasePosition = 0L;

        using (var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false))
        {
            var hashedObjectNames = new HashSet<string>(objectNames);
            archivedObjectItems = EnumerateArchivedObjectDescriptors(archiveFileStream).
                Where(aod => hashedObjectNames.Contains(aod.ObjectName)).
                ToArray();
            archiveFileBasePosition = archiveFileStream.Position;
        }

        Parallel.ForEach(archivedObjectItems,
            aod =>
            {
                aod.AdjustPosition(archiveFileBasePosition);
                
                using var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false);
                
                archiveFileStream.Position = aod.Position;
                var objectStream = new RangedStream(archiveFileStream, aod.Length);
                
                using var outputObjectFileStream = isDryrun ?
                    new NullStream() :
                    StreamUtilities.OpenStream(aod.ObjectName, true);
                objectStream.CopyTo(outputObjectFileStream);
                outputObjectFileStream.Flush();
            });

        foreach (var exceptName in archivedObjectItems.
            Select(aod => aod.ObjectName).
            Except(objectNames))
        {
            this.logger.Error($"Object is not found: {exceptName}");
        }
    }

    internal void List(
        string archiveFilePath,
        string[] objectNames)
    {
        var archivedObjectItems = CommonUtilities.Empty<ArchivedObjectItemDescriptor>();

        using (var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false))
        {
            var hashedObjectNames = new HashSet<string>(objectNames);
            archivedObjectItems = EnumerateArchivedObjectDescriptors(archiveFileStream).
                Where(aod => hashedObjectNames.Contains(aod.ObjectName)).
                ToArray();
        }

        foreach (var objectName in archivedObjectItems)
        {
            Console.WriteLine(objectName);
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
                    this.logger.Information($"creating {Path.GetFileName(options.ArchiveFilePath)}");
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
