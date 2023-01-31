/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace chibias.Internal;

// Imported from ILCompose project.
// https://github.com/kekyo/ILCompose

internal sealed class SymbolReaderProvider : ISymbolReaderProvider
{
    // HACK: cecil will lock symbol file when uses defaulted reading method.
    //   Makes safer around entire building process.

    private static readonly EmbeddedPortablePdbReaderProvider embeddedProvider = new();
    private static readonly MdbReaderProvider mdbProvider = new();
    private static readonly PdbReaderProvider pdbProvider = new();

    private readonly ILogger logger;
    private readonly HashSet<string> loaded = new();
    private readonly HashSet<string> notFound = new();

    public SymbolReaderProvider(ILogger logger) =>
        this.logger = logger;

    private ISymbolReader? TryGetSymbolReader<TSymbolReaderProvider>(
        TSymbolReaderProvider provider, ModuleDefinition module,
        string fullPath, string extension)
        where TSymbolReaderProvider : ISymbolReaderProvider
    {
        var path = Path.Combine(
            Utilities.GetDirectoryPath(fullPath),
            Path.GetFileNameWithoutExtension(fullPath) + extension);

        try
        {
            if (File.Exists(path))
            {
                var ms = new MemoryStream();
                using (var mdbStream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    mdbStream.CopyTo(ms);
                }
                ms.Position = 0;

                var sr = provider.GetSymbolReader(module, ms);
                if (this.loaded.Add(path))
                {
                    this.logger.Debug($"Symbol loaded from: {path}");
                }
                return sr;
            }
        }
        catch (Exception ex)
        {
            this.logger.Warning(ex);
        }

        return null;
    }

    public ISymbolReader? GetSymbolReader(ModuleDefinition module, string fileName)
    {
        if (module.HasDebugHeader)
        {
            var fullPath = Path.GetFullPath(fileName);

            var header = module.GetDebugHeader();
            if (header.Entries.
                FirstOrDefault(e => e.Directory.Type == ImageDebugType.EmbeddedPortablePdb) is { } entry)
            {
                try
                {
                    var sr = embeddedProvider.GetSymbolReader(module, fullPath);
                    if (this.loaded.Add(fullPath))
                    {
                        this.logger.Debug($"Embedded symbol loaded from: {fullPath}");
                    }
                    return sr;
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex);
                }
            }
            else if (this.TryGetSymbolReader(mdbProvider, module, fullPath, ".dll.mdb") is { } sr1)
            {
                return sr1;
            }
            else if (this.TryGetSymbolReader(pdbProvider, module, fullPath, ".pdb") is { } sr3)
            {
                return sr3;
            }

            if (this.notFound.Add(fileName))
            {
                this.logger.Trace($"Symbol not found: {fileName}");
            }
        }

        return null;
    }

    public ISymbolReader? GetSymbolReader(ModuleDefinition module, Stream symbolStream)
    {
        var ms = new MemoryStream();
        symbolStream.CopyTo(ms);
        ms.Position = 0;

        symbolStream.Dispose();

        try
        {
            return embeddedProvider.GetSymbolReader(module, ms);
        }
        catch
        {
        }

        try
        {
            ms.Position = 0;
            return mdbProvider.GetSymbolReader(module, ms);
        }
        catch
        {
        }

        try
        {
            ms.Position = 0;
            return pdbProvider.GetSymbolReader(module, ms);
        }
        catch
        {
        }

        return null;
    }
}
