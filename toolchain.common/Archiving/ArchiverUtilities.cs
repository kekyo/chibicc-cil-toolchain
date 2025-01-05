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

internal interface IObjectFileDescriptor : IObjectItemDescriptor
{
    string Path { get; }
}

public sealed class ObjectFileDescriptor : IObjectFileDescriptor
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

public enum FileTypes
{
    Other,
    ObjectFile,
    SourceFile,
}

public static class ArchiverUtilities
{
    public static readonly string SymbolTableFileName = "__.SYMDEF";
    
    public static FileTypes GetFileType(string path) =>
        Path.GetExtension(path) switch
        {
            ".o" => FileTypes.ObjectFile,
            ".s" => FileTypes.SourceFile,
            _ => FileTypes.Other,
        };

    public static string GetObjectName(string objectFilePath) =>
        GetFileType(objectFilePath) == FileTypes.SourceFile ?
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

    public static IEnumerable<Symbol> EnumerateSymbolsFromObjectFileStream(
        Stream objectFileStream) =>
        InternalEnumerateSymbolsFromObjectFileStream(objectFileStream).
        Distinct();
    
    ////////////////////////////////////////////////////////////////////

    public static Stream ToCompressedObjectStream(
        Stream objectFileStream,
        string referenceObjectFilePath)
    {
        if (GetFileType(referenceObjectFilePath) == FileTypes.ObjectFile)
        {
            if (objectFileStream.CanSeek)
            {
                return objectFileStream;
            }
            var ms = new MemoryStream();
            objectFileStream.CopyTo(ms);
            objectFileStream.Dispose();
            ms.Position = 0;
            return ms;
        }
        else
        {
            var gzs = new GZipStream(objectFileStream, CompressionLevel.Optimal);
            var ms = new MemoryStream();
            gzs.CopyTo(ms);
            objectFileStream.Dispose();
            ms.Position = 0;
            return ms;
        }
    }

    public static Stream OpenCompressedObjectStream(string objectFilePath)
    {
        var ofs = StreamUtilities.OpenStream(objectFilePath, false);
        return ToCompressedObjectStream(ofs, objectFilePath);
    }

    public static Stream ToDecompressedObjectStream(
        Stream objectFileStream,
        string referenceObjectFilePath)
    {
        if (GetFileType(referenceObjectFilePath) != FileTypes.ObjectFile)
        {
            if (objectFileStream.CanSeek)
            {
                return objectFileStream;
            }
            var ms = new MemoryStream();
            objectFileStream.CopyTo(ms);
            objectFileStream.Dispose();
            ms.Position = 0;
            return ms;
        }
        else
        {
            var gzs = new GZipStream(objectFileStream, CompressionMode.Decompress);
            var ms = new MemoryStream();
            gzs.CopyTo(ms);
            objectFileStream.Dispose();
            ms.Position = 0;
            return ms;
        }
    }

    public static Stream OpenDecompressedObjectStream(string objectFilePath)
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

    ////////////////////////////////////////////////////////////////////

    public static IEnumerable<SymbolList> EnumerateSymbolListFromArchive(
        this ArchiveReader archiveReader)
    {
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
}
