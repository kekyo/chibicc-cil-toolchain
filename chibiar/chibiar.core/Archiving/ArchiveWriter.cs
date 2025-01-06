/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Archiving;
using chibicc.toolchain.Internal;
using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace chibiar.Archiving;

public static class ArchiveWriter
{
    private sealed class WillUpdateObjectFileDescriptor : IObjectFileDescriptor
    {
        public int Length =>
            throw new InvalidOperationException();
        public string ObjectName =>
            ArchiverUtilities.GetObjectName(this.Path);
        public string Path { get; }
        
        public WillUpdateObjectFileDescriptor(string path) =>
            this.Path = path;
    }

    public static SymbolListEntry[] GetCombinedSymbolListEntries(
        ILogger logger,
        string archiveFilePath,
        string[] objectFilePaths)
    {
        using var scope = logger.BeginScope(LogLevels.Debug);

        var archivedObjectItems = CommonUtilities.Empty<IObjectItemDescriptor>();

        if (File.Exists(archiveFilePath))
        {
            var hashedObjectNames = objectFilePaths.ToDictionary(ArchiverUtilities.GetObjectName);
            archivedObjectItems = ArchiverUtilities.LoadArchivedObjectItemDescriptors(
                archiveFilePath,
                aod => hashedObjectNames.TryGetValue(aod.ObjectName, out var path) ?
                    new WillUpdateObjectFileDescriptor(path) :   // Will update object file
                    aod.ObjectName == ArchiverUtilities.SymbolTableFileName ? 
                        null :                                   // Will ignore symbol table
                        aod);                                    // Archived object file
        }

        scope.Debug("Step 1");

        // Aggregate and extract to add new object files
        var willAddNewObjectFileItems = objectFilePaths.
            Except(archivedObjectItems.
                OfType<WillUpdateObjectFileDescriptor>().
                Select(d => d.Path)).
            Select(path => new WillUpdateObjectFileDescriptor(path)).
            ToArray();

        var symbolListEntries = new SymbolListEntry[
            archivedObjectItems.Length + willAddNewObjectFileItems.Length];

        Parallel.ForEach(
            archivedObjectItems.Concat(willAddNewObjectFileItems),
            (entry, _, index) =>
            {
                switch (entry)
                {
                    case IObjectFileDescriptor ofd:
                        using (var objectRawStream = StreamUtilities.OpenStream(ofd.Path, false))
                        {
                            var length = objectRawStream.Length;
                            var objectStream = ArchiverUtilities.ToDecompressedObjectStream(objectRawStream, ofd.Path);
                            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFileStream(objectStream).
                                ToArray();
                            symbolListEntries[index] = new(
                                new ObjectFileDescriptor((int)length, ofd.Path),
                                new(ofd.ObjectName, symbols));
                        }
                        break;
                    case ArchivedObjectItemDescriptor aod:
                        using (var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath!, false))
                        {
                            archiveFileStream.Position = aod.Position;
                            var objectStream = ArchiverUtilities.ToDecompressedObjectStream(
                                new RangedStream(archiveFileStream, aod.Length, false),
                                aod.ObjectName);
                            var symbols = ArchiverUtilities.EnumerateSymbolsFromObjectFileStream(objectStream).
                                ToArray();
                            symbolListEntries[index] = new(aod, new(aod.ObjectName, symbols));
                        }
                        break;
                }

                scope.Debug($"Step 2: {entry.ObjectName}");
            });

        scope.Debug($"Step 3");

        return symbolListEntries;
    }

    ////////////////////////////////////////////////////////////////////

    private static void WriteSymbolTable(
        Stream symbolTableStream,
        IEnumerable<SymbolList> symbolLists)
    {
        var tw = StreamUtilities.CreateTextWriter(symbolTableStream);
        foreach (var symbolList in symbolLists)
        {
            tw.WriteLine(
                $".object {symbolList.ObjectName}");
            foreach (var symbol in symbolList.Symbols.Distinct())
            {
                tw.WriteLine(
                    $"    {symbol.Directive} {symbol.Scope} {symbol.Name}{(symbol.MemberCount is { } mc ? $" {mc}" : "")}");
            }
        }
        tw.Flush();
    }

    private static byte[] CreateSymbolTableImage(
        IEnumerable<SymbolListEntry> symbolListEntries)
    {
        var symbolTableStream = new MemoryStream();
        using (var compressedStream = new GZipStream(symbolTableStream, CompressionLevel.Fastest))
        {
            WriteSymbolTable(
                compressedStream,
                symbolListEntries.Select(entry => entry.SymbolList));
            compressedStream.Flush();
        }
        return symbolTableStream.ToArray();
    }

    ////////////////////////////////////////////////////////////////////

    private static void WriteObjectItemDescriptors(
        Stream outputArchiveFileStream,
        IEnumerable<IObjectItemDescriptor> descriptors)
    {
        var lew = new LittleEndianWriter(outputArchiveFileStream);
        
        // Head of the identity.
        lew.Write(ArchiverUtilities.ArchiveIdentity);
        
        // Descriptors.
        foreach (var descriptor in descriptors)
        {
            lew.Write(descriptor.ObjectName);
            lew.Write(descriptor.Length);
        }
        
        // Descriptor termination.
        lew.Write(string.Empty);
    }

    private static void WriteObjectItemBodies(
        ILogger logger,
        Stream outputArchiveFileStream,
        string readArchiveFilePath,
        IEnumerable<IObjectItemDescriptor> descriptors)
    {
        using var scope = logger.BeginScope(LogLevels.Debug);

        foreach (var descriptor in descriptors)
        {
            switch (descriptor)
            {
                case ObjectFileDescriptor ofd:
                    using (var objectFileStream = ArchiverUtilities.OpenCompressedObjectStream(ofd.Path))
                    {
                        objectFileStream.CopyTo(outputArchiveFileStream);
                    }
                    break;
                case ArchivedObjectItemDescriptor aod:
                    using (var archiveFileStream = StreamUtilities.OpenStream(readArchiveFilePath, false))
                    {
                        archiveFileStream.Position = aod.Position;
                        var objectStream = new RangedStream(archiveFileStream, aod.Length, false);
                        objectStream.CopyTo(outputArchiveFileStream);
                    }
                    break;
            }

            scope.Debug($"Step 1: {descriptor.ObjectName}");
        }

        scope.Debug($"Step 2");
    }

    private sealed class SymbolTableDescriptor(int length) : IObjectItemDescriptor
    {
        public int Length =>
            length;
        public string ObjectName =>
            ArchiverUtilities.SymbolTableFileName;
    }

    public static void WriteArchive(
        ILogger logger,
        Stream outputArchiveFileStream,
        SymbolListEntry[] symbolListEntries,
        string readArchiveFilePath,
        bool writeSymbolTable)
    {
        using var scope = logger.BeginScope(LogLevels.Debug);

        if (writeSymbolTable)
        {
            // Create symbol table image (and get that length).
            var symbolTableImage = CreateSymbolTableImage(symbolListEntries);

            scope.Debug("Step 1");

            // Write the descriptors to the head of the archive file.
            WriteObjectItemDescriptors(
                outputArchiveFileStream,
                symbolListEntries.
                    Select(entry => entry.Descriptor).
                    // Insert the symbol table descriptor to the front.
                    Prepend(new SymbolTableDescriptor(symbolTableImage.Length)));

            // Write the symbol table image next to the descriptors.
            outputArchiveFileStream.Write(
                symbolTableImage, 0, symbolTableImage.Length);
        }
        else
        {
            // Write the descriptors to the head of the archive file.
            WriteObjectItemDescriptors(
                outputArchiveFileStream,
                symbolListEntries.Select(entry => entry.Descriptor));
        }

        scope.Debug("Step 2");

        // Write all required object items.
        WriteObjectItemBodies(
            logger,
            outputArchiveFileStream,
            readArchiveFilePath,
            symbolListEntries.Select(entry => entry.Descriptor));

        scope.Debug("Step 3");
    }
}
