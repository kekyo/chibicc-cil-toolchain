/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using chibicc.toolchain.IO;
using chibicc.toolchain.Parsing;
using chibicc.toolchain.Tokenizing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace chibicc.toolchain.Archiving;

public interface IObjectItemDescriptor
{
    int Length { get; }
    string ObjectName { get; }
}

public sealed class ArchivedObjectItemDescriptor : IObjectItemDescriptor
{
    public long Position { get; private set; }
    public int Length { get; }
    public string ObjectName { get; }

    internal ArchivedObjectItemDescriptor(long relativePosition, int length, string objectName)
    {
        this.Position = relativePosition;
        this.Length = length;
        this.ObjectName = objectName;
    }

    internal void AdjustPosition(long basePosition) =>
        this.Position += basePosition;
}

public sealed class ObjectFileDescriptor : IObjectItemDescriptor
{
    public int Length { get; }
    public string Path { get; }
    public string ObjectName =>
        ArchiverUtilities.GetObjectName(this.Path);

    internal ObjectFileDescriptor(int length, string path)
    {
        this.Length = length;
        this.Path = path;
    }
}

public record struct SymbolListEntry(
    IObjectItemDescriptor Descriptor,
    SymbolList SymbolList);

public static class ArchiverUtilities
{
    public static readonly string SymbolTableFileName = "__.SYMDEF";
    
    public static bool IsSourceFile(string path) =>
        Path.GetExtension(path) is not ".o";

    public static string GetObjectName(string objectFilePath) =>
        IsSourceFile(objectFilePath) ?
            (Path.GetFileNameWithoutExtension(objectFilePath) + ".o") :
            Path.GetFileName(objectFilePath);

    private enum ObjectSymbolStates
    {
        Idle,
        Enumeration,
        Structure,
    }
    
    private static IEnumerable<Symbol> InternalEnumerateSymbolsFromObjectFileStream(
        Stream objectFileStream)
    {
        var tr = StreamUtilities.CreateTextReader(objectFileStream);

        var state = ObjectSymbolStates.Idle;
        string? currentDirective = null;
        string? currentScope = null;
        string? currentName = null;
        var members = new List<Token[]>();

        foreach (var tokens in CilTokenizer.TokenizeAll("", SymbolTableFileName, tr))
        {
            if (tokens.Length >= 3 &&
                tokens[0] is (TokenTypes.Directive, var directive and ("function" or "global" or "enumeration" or "structure")) &&
                tokens[1] is (TokenTypes.Identity, var scope) &&
                CommonUtilities.TryParseEnum<Scopes>(scope, out _))
            {
                switch (state)
                {
                    case ObjectSymbolStates.Enumeration:
                    case ObjectSymbolStates.Structure:
                        Debug.Assert(currentDirective != null);
                        Debug.Assert(currentScope != null);
                        Debug.Assert(currentName != null);
                        yield return new(
                            currentDirective!,
                            currentScope!,
                            currentName!,
                            members.Count);
                        state = ObjectSymbolStates.Idle;
                        currentScope = null;
                        currentName = null;
                        currentDirective = null;
                        members.Clear();
                        break;
                }
                
                switch (directive)
                {
                    // .function public int32() funcfoo
                    // .global public int32 varbar
                    case "function":
                    case "global":
                        if (tokens.Length >= 4 &&
                            tokens[3] is (TokenTypes.Identity, var name1))
                        {
                            yield return new(directive, scope, name1, null);
                        }
                        break;
                    // .enumeration public int32 enumbaz
                    case "enumeration":
                        if (tokens.Length >= 4 &&
                            tokens[3] is (TokenTypes.Identity, var name2))
                        {
                            currentDirective = directive;
                            currentScope = scope;
                            currentName = name2;
                            members.Clear();
                            state = ObjectSymbolStates.Enumeration;
                        }
                        break;
                    // .structure public structhoge
                    case "structure":
                        if (tokens[2] is (TokenTypes.Identity, var name3))
                        {
                            currentDirective = directive;
                            currentScope = scope;
                            currentName = name3;
                            members.Clear();
                            state = ObjectSymbolStates.Structure;
                        }
                        break;
                }
            }
            else if (state != ObjectSymbolStates.Idle &&
                tokens.Length >= 1 &&
                tokens[0] is (TokenTypes.Identity, _))
            {
                members.Add(tokens);
            }
        }
        
        switch (state)
        {
            case ObjectSymbolStates.Enumeration:
            case ObjectSymbolStates.Structure:
                Debug.Assert(currentDirective != null);
                Debug.Assert(currentScope != null);
                Debug.Assert(currentName != null);
                yield return new(
                    currentDirective!,
                    currentScope!,
                    currentName!,
                    members.Count);
                state = ObjectSymbolStates.Idle;
                break;
        }
    }

    private static IEnumerable<Symbol> EnumerateSymbolsFromObjectFileStream(
        Stream objectFileStream) =>
        InternalEnumerateSymbolsFromObjectFileStream(objectFileStream).
        Distinct();
    
    ////////////////////////////////////////////////////////////////////

    private static Stream ToCompressedObjectStream(
        Stream objectFileStream,
        string referenceObjectFilePath)
    {
        if (!IsSourceFile(referenceObjectFilePath))
        {
            return objectFileStream;
        }
        var gzs = new GZipStream(objectFileStream, CompressionLevel.Optimal);
        var ms = new MemoryStream();
        gzs.CopyTo(ms);
        objectFileStream.Dispose();
        ms.Position = 0;
        return ms;
    }

    private static Stream OpenCompressedObjectStream(string objectFilePath)
    {
        var ofs = StreamUtilities.OpenStream(objectFilePath, false);
        return ToCompressedObjectStream(ofs, objectFilePath);
    }

    private static Stream ToDecompressedObjectStream(
        Stream objectFileStream,
        string referenceObjectFilePath)
    {
        if (IsSourceFile(referenceObjectFilePath))
        {
            return objectFileStream;
        }
        var gzs = new GZipStream(objectFileStream, CompressionMode.Decompress);
        var ms = new MemoryStream();
        gzs.CopyTo(ms);
        objectFileStream.Dispose();
        ms.Position = 0;
        return ms;
    }

    private static Stream OpenDecompressedObjectStream(string objectFilePath)
    {
        var ofs = StreamUtilities.OpenStream(objectFilePath, false);
        return ToDecompressedObjectStream(ofs, objectFilePath);
    }

    ////////////////////////////////////////////////////////////////////

    private static IEnumerable<ArchivedObjectItemDescriptor> EnumerateArchivedObjectItemDescriptors(
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
                // Detected termination.
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

    public static IObjectItemDescriptor[] LoadArchivedObjectItemDescriptors(
        string archiveFilePath,
        Func<ArchivedObjectItemDescriptor, IObjectItemDescriptor?> selector)
    {
        using var archiveFileStream = StreamUtilities.OpenStream(archiveFilePath, false);
        var descriptors = EnumerateArchivedObjectItemDescriptors(archiveFileStream).
            Select(aod => selector(aod)!).
            Where(d => d != null).
            ToArray();
        var archiveFileBasePosition = archiveFileStream.Position;
        foreach (var descriptor in descriptors)
        {
            if (descriptor is ArchivedObjectItemDescriptor aod)
            {
                aod.AdjustPosition(archiveFileBasePosition);
            }
        }
        return descriptors;
    }

    public static SymbolListEntry[] GetCombinedSymbolListEntries(
        string archiveFilePath,
        string[] objectFilePaths)
    {
        var archivedObjectItems = CommonUtilities.Empty<IObjectItemDescriptor>();

        if (File.Exists(archiveFilePath))
        {
            var hashedObjectNames = objectFilePaths.ToDictionary(GetObjectName);
            archivedObjectItems = LoadArchivedObjectItemDescriptors(
                archiveFilePath,
                aod => hashedObjectNames.TryGetValue(aod.ObjectName, out var path) ?
                    new ObjectFileDescriptor(-1, path) :      // Will update object file
                    aod.ObjectName == SymbolTableFileName ? 
                        null :                                // Will ignore symbol table
                        aod);                                 // Archived object file
        }
        
        var willAddObjectFileItems = objectFilePaths.
            Except(archivedObjectItems.
                OfType<ObjectFileDescriptor>().
                Select(d => d.Path)).
            Select(path => new ObjectFileDescriptor(-1, path)).
            ToArray();

        var symbolListEntries = new SymbolListEntry[
            archivedObjectItems.Length + willAddObjectFileItems.Length];

        Parallel.ForEach(
            archivedObjectItems.Concat(willAddObjectFileItems),
            (entry, _, index) =>
            {
                switch (entry)
                {
                    case ObjectFileDescriptor ofd:
                        using (var objectRawStream = StreamUtilities.OpenStream(ofd.Path, false))
                        {
                            var length = objectRawStream.Length;
                            var objectStream = ToDecompressedObjectStream(objectRawStream, ofd.Path);
                            var symbols = EnumerateSymbolsFromObjectFileStream(objectStream).
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
                            var objectStream = ToDecompressedObjectStream(
                                new RangedStream(archiveFileStream, aod.Length, false),
                                aod.ObjectName);
                            var symbols = EnumerateSymbolsFromObjectFileStream(objectStream).
                                ToArray();
                            symbolListEntries[index] = new(aod, new(aod.ObjectName, symbols));
                        }
                        break;
                }
            });

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
        foreach (var descriptor in descriptors)
        {
            lew.Write(descriptor.ObjectName);
            lew.Write(descriptor.Length);
        }
        lew.Write(string.Empty);   // Termination
    }

    private static void WriteObjectItemBodies(
        Stream outputArchiveFileStream,
        string readArchiveFilePath,
        IEnumerable<IObjectItemDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            switch (descriptor)
            {
                case ObjectFileDescriptor ofd:
                    using (var objectFileStream = OpenCompressedObjectStream(ofd.Path))
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
        }
    }

    private sealed class SymbolTableDescriptor(int length) : IObjectItemDescriptor
    {
        public int Length =>
            length;
        public string ObjectName =>
            SymbolTableFileName;
    }

    public static void WriteArchive(
        Stream outputArchiveFileStream,
        SymbolListEntry[] symbolListEntries,
        string readArchiveFilePath,
        bool writeSymbolTable)
    {
        if (writeSymbolTable)
        {
            var symbolTableStream = new MemoryStream(
                CreateSymbolTableImage(symbolListEntries));

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

        WriteObjectItemBodies(
            outputArchiveFileStream,
            readArchiveFilePath,
            symbolListEntries.Select(entry => entry.Descriptor));

        outputArchiveFileStream.Flush();
    }

    ////////////////////////////////////////////////////////////////////

    public static IEnumerable<SymbolList> EnumerateSymbolListFromArchive(
        string archiveFilePath)
    {
        var archiveReader = new ArchiveReader(
            archiveFilePath, [ SymbolTableFileName ]);

        if (archiveReader.TryOpenObjectStream(
            SymbolTableFileName, true, out var symbolTableStream))
        {
            var tr = StreamUtilities.CreateTextReader(symbolTableStream);

            Token? currentObjectName = null;
            var symbols = new List<Symbol>();            

            foreach (var tokens in CilTokenizer.TokenizeAll(
                "", SymbolTableFileName, tr).
                Where(tokens => tokens.Length >= 2))
            {
                switch (tokens[0])
                {
                    case (TokenTypes.Directive, "object")
                        when tokens[1] is (TokenTypes.Identity, _):
                        if (currentObjectName is (_, var objectName))
                        {
                            yield return new(
                                objectName,
                                symbols.Distinct().ToArray());
                        }
                        currentObjectName = tokens[1];
                        symbols.Clear();
                        break;
                    // function public funcfoo
                    // global public varbar
                    // enumeration public enumbaz 3
                    // structure public structhoge 5
                    case (TokenTypes.Identity, var directive)
                        when tokens is [_, (TokenTypes.Identity, var scope), _, ..] &&
                             CommonUtilities.TryParseEnum<Scopes>(scope, out _) &&
                             tokens[2] is (TokenTypes.Identity, var name):
                        if (tokens is [_, _, _, (TokenTypes.Identity, var mc), ..] &&
                            int.TryParse(mc, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memberCount) &&
                            memberCount >= 0)
                        {
                            symbols.Add(new(directive, scope, name, memberCount));
                        }
                        else
                        {
                            symbols.Add(new(directive, scope, name, null));
                        }
                        break;
                }
            }
            if (currentObjectName is var (_, con2))
            {
                yield return new(
                    con2,
                    symbols.Distinct().ToArray());
            }
        }
    }

    public static bool TryOpenArchivedObject(
        string archiveFilePath,
        string objectName,
        bool decodedBody,
        out Stream stream)
    {
        var archiveReader = new ArchiveReader(
            archiveFilePath, [ objectName ]);
        return archiveReader.TryOpenObjectStream(
            objectName, decodedBody, out stream);
    }
}
