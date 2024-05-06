/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Threading;
using chibicc.toolchain.Logging;
using Mono.Cecil;

namespace chibild.Internal;

// Imported from ILCompose project.
// https://github.com/kekyo/ILCompose

internal sealed class CachedAssemblyResolver : DefaultAssemblyResolver
{
    private static int index;

    private readonly ILogger logger;
    private readonly Dictionary<string, AssemblyDefinition> byPath = new();
    private readonly Dictionary<string, AssemblyDefinition> byFullName = new();
    private readonly MultipleSymbolReaderProvider symbolReaderProvider;
    private readonly ReaderParameters parameters;
    private readonly int instanceIndex;

    public CachedAssemblyResolver(
        ILogger logger,
        ReadingMode readingMode,
        string[] referenceBasePaths)
    {
        this.instanceIndex = Interlocked.Increment(ref index);

        this.logger = logger;
        this.symbolReaderProvider = new MultipleSymbolReaderProvider(this.logger);
        this.parameters = new ReaderParameters(readingMode)
        {
            ReadWrite = false,
            InMemory = true,
            AssemblyResolver = this,
            ReadSymbols = true,
            SymbolReaderProvider = this.symbolReaderProvider,
            ThrowIfSymbolsAreNotMatching = false,
        };

        foreach (var referenceBasePath in referenceBasePaths)
        {
            var fullPath = Path.GetFullPath(referenceBasePath);
            base.AddSearchDirectory(fullPath);
            this.logger.Trace($"Reference base path: {fullPath}");
        }
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        if (!this.byFullName.TryGetValue(name.FullName, out var assembly))
        {
            assembly = base.Resolve(name, this.parameters);
            this.byPath[assembly.MainModule.FileName] = assembly;
            this.byFullName[assembly.Name.FullName] = assembly;
        }
        return assembly;
    }

    public AssemblyDefinition ReadAssemblyFrom(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        
        if (!this.byPath.TryGetValue(fullPath, out var assembly))
        {
            assembly = AssemblyDefinition.ReadAssembly(assemblyPath, this.parameters);
            this.byPath[fullPath] = assembly;
            this.byPath[assembly.MainModule.FileName] = assembly;
            this.byFullName[assembly.Name.FullName] = assembly;
        }
        return assembly;
    }

    public override string ToString() =>
        $"CachedAssemblyResolver: Index={this.instanceIndex}, ByPath={this.byPath.Count}, ByFullName={this.byFullName.Count}";
}
