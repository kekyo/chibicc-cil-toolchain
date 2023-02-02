/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace chibias;

public sealed class Assembler
{
    private readonly ILogger logger;
    private readonly AssemblyResolver assemblyResolver;

    public Assembler(
        ILogger logger,
        params string[] referenceAssemblyBasePaths)
    {
        this.logger = logger;
        this.assemblyResolver = new AssemblyResolver(
            this.logger,
            referenceAssemblyBasePaths);
    }

    private TypeDefinition[] LoadPublicTypesFrom(
        string[] referenceAssemblyPaths)
    {
        var assemblies = referenceAssemblyPaths.
            Distinct().
            Select(this.assemblyResolver.ReadAssemblyFrom).
            ToArray();

        IEnumerable<AssemblyDefinition> ResolveDescendants(
            AssemblyNameReference anr, HashSet<AssemblyNameReference> saved)
        {
            if (saved.Add(anr) &&
                this.assemblyResolver.Resolve(anr) is { } assembly)
            {
                return new[] { assembly }.
                    Concat(assembly.Modules.
                        SelectMany(module => module.AssemblyReferences).
                        SelectMany(anr => ResolveDescendants(anr, saved)));
            }
            else
            {
                return Utilities.Empty<AssemblyDefinition>();
            }
        }

        var saved = new HashSet<AssemblyNameReference>(
            AssemblyNameReferenceComparer.Instance);

        var corlibAssemblies = assemblies.
            Collect(assembly => assembly.MainModule.TypeSystem.CoreLibrary as AssemblyNameReference).
            SelectMany(anr => ResolveDescendants(anr, saved)).
            ToArray();

        return corlibAssemblies.
            Concat(assemblies).
            SelectMany(assembly => assembly.Modules).
            SelectMany(module => module.Types).
            Distinct(TypeDefinitionComparer.Instance).
            Collect(type => type?.Resolve()).
            Where(type =>
                type.IsPublic &&
                (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum) &&
                type.GenericParameters.Count == 0).
            ToArray();
    }

    private Dictionary<string, IMemberDefinition> AggregateCAbiSpecificSymbols(
        TypeDefinition[] referenceTypes)
    {
        return referenceTypes.
            Where(type =>
                type.IsClass && type.IsAbstract && type.IsSealed &&
                type.Namespace == "C").
            SelectMany(type =>
            {
                var methods = type.Methods.
                    Where(method => method.IsPublic && method.IsStatic && !method.HasGenericParameters).
                    Select(method => (IMemberDefinition)method).
                    ToArray();
                var fields = type.Fields.
                    Where(field => field.IsPublic && field.IsStatic).
                    Select(field => (IMemberDefinition)field).
                    ToArray();
                var types = type.NestedTypes.
                    Where(type => type.IsPublic && type.IsValueType).
                    Select(type => (IMemberDefinition)type).
                    ToArray();
                return methods.Concat(fields).Concat(types).ToArray();
            }).
            // Ignored trailing existence symbol names.
            Distinct(MemberDefinitionNameComparer.Instance).
            ToDictionary(member => member.Name);
    }

    private void AssembleFromSource(
        Parser parser,
        string? baseSourcePath,
        string sourcePathDebuggerHint,
        TextReader sourceCodeReader)
    {
        parser.SetSourcePathDebuggerHint(
            baseSourcePath,
            sourcePathDebuggerHint);

        var tokenizer = new Tokenizer();

        while (true)
        {
            var line = sourceCodeReader.ReadLine();
            if (line == null)
            {
                break;
            }

            var tokens = tokenizer.TokenizeLine(line);
            parser.Parse(tokens);
        }
    }

    private bool Run(
        string outputAssemblyPath,
        AssemblerOptions options,
        Action<Parser> runner)
    {
        var outputAssemblyFullPath = Path.GetFullPath(outputAssemblyPath);

        var referenceTypes = this.LoadPublicTypesFrom(
            options.ReferenceAssemblyPaths);

        var cabiSpecificSymbols = this.AggregateCAbiSpecificSymbols(
            referenceTypes);

        //////////////////////////////////////////////////////////////

        var assemblyName = new AssemblyNameDefinition(
            Path.GetFileNameWithoutExtension(outputAssemblyFullPath),
            options.Version);
        var assembly = AssemblyDefinition.CreateAssembly(
            assemblyName,
            Path.GetFileName(outputAssemblyFullPath),
            options.AssemblyType switch
            {
                AssemblyTypes.Dll => ModuleKind.Dll,
                AssemblyTypes.WinExe => ModuleKind.Windows,
                _ => ModuleKind.Console
            });

        var module = assembly.MainModule;

        //////////////////////////////////////////////////////////////

        var produceExecutable =
            options.AssemblyType != AssemblyTypes.Dll;

        var parser = new Parser(
            this.logger,
            module,
            cabiSpecificSymbols,
            referenceTypes,
            produceExecutable,
            options.DebugSymbolType != DebugSymbolTypes.None);

        runner(parser);

        var allFinished = parser.Finish(
            options.Options.HasFlag(AssembleOptions.ApplyOptimization));

        //////////////////////////////////////////////////////////////

        if (allFinished)
        {
            this.logger.Information($"Writing: {Path.GetFileName(outputAssemblyFullPath)}");

            module.Write(
                outputAssemblyFullPath,
                new()
                {
                    DeterministicMvid =
                        options.Options.HasFlag(AssembleOptions.Deterministic),
                    WriteSymbols =
                        options.DebugSymbolType != DebugSymbolTypes.None,
                    SymbolWriterProvider = options.DebugSymbolType switch
                    {
                        DebugSymbolTypes.None => null!,
                        DebugSymbolTypes.Embedded => new EmbeddedPortablePdbWriterProvider(),
                        DebugSymbolTypes.Mono => new MdbWriterProvider(),
                        DebugSymbolTypes.WindowsProprietary => new NativePdbWriterProvider(),
                        _ => new PortablePdbWriterProvider(),
                    },
                });
        }

        return allFinished;
    }

    public bool Assemble(
        string outputAssemblyPath,
        AssemblerOptions options,
        string sourcePathDebuggerHint,
        TextReader sourceCodeReader) =>
        this.Run(
            outputAssemblyPath,
            options,
            parser =>
            {
                this.logger.Information($"Assembling: {sourcePathDebuggerHint}");

                this.AssembleFromSource(
                    parser,
                    null,
                    sourcePathDebuggerHint,
                    sourceCodeReader);
            });

    public bool Assemble(
        string outputAssemblyPath,
        AssemblerOptions options,
        params string[] sourcePaths)
    {
        if (sourcePaths.Length == 0)
        {
            return false;
        }

        var sourceFullPaths = sourcePaths.
            Select(path => path != "-" ? Path.GetFullPath(path) : path).
            ToArray();

        var baseSourcePath = sourceFullPaths.Length == 1 ?
            Utilities.GetDirectoryPath(sourceFullPaths[0]) :
            Path.Combine(sourceFullPaths.
                Select(path => path.Split(Path.DirectorySeparatorChar)).
                Aggregate((path0, path1) => path0.Intersect(path1).ToArray()));  // Intersect is stable?

        this.logger.Information($"Source code base path: {baseSourcePath}");

        //////////////////////////////////////////////////////////////

        return this.Run(
            outputAssemblyPath,
            options,
            parser =>
            {
                foreach (var sourceFullPath in sourceFullPaths)
                {
                    if (sourceFullPath != "-")
                    {
                        using var fs = new FileStream(
                            sourceFullPath,
                            FileMode.Open, FileAccess.Read, FileShare.Read);
                        var reader = new StreamReader(
                            fs,
                            true);

                        var sourcePathDebuggerHint = sourceFullPath.
                            Substring(baseSourcePath.Length + 1);

                        this.logger.Information($"Assembling: {sourcePathDebuggerHint}");

                        this.AssembleFromSource(
                            parser,
                            baseSourcePath,
                            sourcePathDebuggerHint,
                            reader);
                    }
                    else
                    {
                        this.logger.Information("Assembling: <stdin>");

                        this.AssembleFromSource(
                            parser,
                            null,
                            "<stdin>",
                            Console.In);
                    }
                }
            });
    }
}
