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
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Archiving;

public static class ArchiverUtilities
{
    public static readonly string SymbolTableFileName = "__symtable$";
    
    public static IEnumerable<Symbol> EnumerateSymbols(TextReader tr, string fileName)
    {
        var tokenizer = new Tokenizer();

        while (true)
        {
            var line = tr.ReadLine();
            if (line == null)
            {
                break;
            }

            var tokens = tokenizer.TokenizeLine(line);
            if (tokens.Length >= 3)
            {
                var directive = tokens[0];
                var scope = tokens[1];
                if (directive.Type == TokenTypes.Directive &&
                    scope.Type == TokenTypes.Identity &&
                    scope.Text is "public" or "internal")
                {
                    switch (directive.Text)
                    {
                        case "function":
                        case "global":
                        case "enumeration":
                            if (tokens.Length >= 4)
                            {
                                yield return new(directive, scope, tokens[3], fileName);
                            }
                            break;
                        case "structure":
                            yield return new(directive, scope, tokens[2], fileName);
                            break;
                    }
                }
            }
        }
    }
    
    public static void WriteSymbolTable(Stream stream, Symbol[] symbols)
    {
        var tw = new StreamWriter(stream, Encoding.UTF8);

        foreach (var symbol in symbols)
        {
            tw.WriteLine($".{symbol.Directive.Text} {symbol.Name.Text} {symbol.FileName}");
        }

        tw.Flush();
    }

    public static string[] EnumerateArchiveItem(string archiveFilePath)
    {
        using var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        return archive.Entries.
            Where(entry => entry.Name != SymbolTableFileName).
            Select(entry => entry.Name).
            ToArray();
    }

    private sealed class ArchiveItemStream : Stream
    {
        private readonly ZipArchive archive;
        private readonly Stream parent;
        
        public ArchiveItemStream(ZipArchive archive, Stream parent)
        {
            this.archive = archive;
            this.parent = parent;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.parent.Dispose();
            this.archive.Dispose();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            this.parent.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new System.NotImplementedException();

        public override void SetLength(long value) =>
            throw new System.NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new System.NotImplementedException();
    }

    public static Stream OpenArchiveItem(string archiveFilePath, string itemName)
    {
        using var archive = ZipFile.Open(
            archiveFilePath,
            ZipArchiveMode.Read,
            Encoding.UTF8);

        return new ArchiveItemStream(archive, archive.GetEntry(itemName)!.Open());
    }
}
