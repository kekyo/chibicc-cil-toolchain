﻿/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Collections.Generic;
using System.IO;

namespace chibias.Internal;

// Imported from ILCompose project.
// https://github.com/kekyo/ILCompose

internal sealed class AssemblyResolver : DefaultAssemblyResolver
{
    private readonly ILogger logger;
    private readonly Dictionary<string, AssemblyDefinition> loadedAssemblies = new();
    private readonly SymbolReaderProvider symbolReaderProvider;

    public AssemblyResolver(ILogger logger, string[] referenceBasePaths)
    {
        this.logger = logger;
        this.symbolReaderProvider = new SymbolReaderProvider(this.logger);

        foreach (var referenceBasePath in referenceBasePaths)
        {
            var fullPath = Path.GetFullPath(referenceBasePath);
            base.AddSearchDirectory(fullPath);
            this.logger.Trace($"Reference base path: {fullPath}");
        }
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        if (!this.loadedAssemblies.TryGetValue(name.Name, out var assmebly))
        {
            var parameters = new ReaderParameters()
            {
                ReadWrite = false,
                InMemory = true,
                AssemblyResolver = this,
                SymbolReaderProvider = this.symbolReaderProvider,
                ReadSymbols = true,
            };
            try
            {
                assmebly = base.Resolve(name, parameters);
                this.logger.Trace($"Assembly loaded: {assmebly.MainModule.FileName}");
            }
            catch
            {
            }
        }
        return assmebly!;
    }

    public AssemblyDefinition ReadAssemblyFrom(string assemblyName)
    {
        if (!this.loadedAssemblies.TryGetValue(assemblyName, out var assmebly))
        {
            var parameters = new ReaderParameters()
            {
                ReadWrite = false,
                InMemory = true,
                AssemblyResolver = this,
                SymbolReaderProvider = this.symbolReaderProvider,
                ReadSymbols = true,
            };
            try
            {
                assmebly = AssemblyDefinition.ReadAssembly(assemblyName, parameters);
                this.logger.Trace($"Assembly loaded: {assmebly.MainModule.FileName}");
            }
            catch
            {
            }
        }
        return assmebly!;
    }
}
