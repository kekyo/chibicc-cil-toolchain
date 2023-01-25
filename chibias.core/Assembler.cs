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
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace chibias;

public enum AssemblyTypes
{
    Dll,
    Exe,
    WinExe,
}

public enum DebugSymbolTypes
{
    None,
    Portable,
    Embedded,
    WindowsProprietary,
}

[Flags]
public enum AssembleOptions
{
    None = 0x00,
    ApplyOptimization = 0x01,
    Deterministic = 0x02,
}

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

    private sealed class AssemblyDefinitionComparer : IEqualityComparer<AssemblyDefinition>
    {
        public bool Equals(AssemblyDefinition? x, AssemblyDefinition? y) =>
            x!.Name == y!.Name;

        public int GetHashCode(AssemblyDefinition obj) =>
            obj.Name.GetHashCode();

        public static readonly AssemblyDefinitionComparer Instance = new();
    }

    private sealed class AssemblyNameReferenceComparer : IEqualityComparer<AssemblyNameReference>
    {
        public bool Equals(AssemblyNameReference? x, AssemblyNameReference? y) =>
            x!.Name == y!.Name;

        public int GetHashCode(AssemblyNameReference obj) =>
            obj.Name.GetHashCode();

        public static readonly AssemblyNameReferenceComparer Instance = new();
    }

    private sealed class ExportedTypeComparer : IEqualityComparer<ExportedType>
    {
        public bool Equals(ExportedType? x, ExportedType? y) =>
            x!.FullName == y!.FullName;

        public int GetHashCode(ExportedType obj) =>
            obj.FullName.GetHashCode();

        public static readonly ExportedTypeComparer Instance = new();
    }

    private sealed class TypeDefinitionComparer : IEqualityComparer<TypeDefinition>
    {
        public bool Equals(TypeDefinition? x, TypeDefinition? y) =>
            x!.FullName == y!.FullName;

        public int GetHashCode(TypeDefinition obj) =>
            obj.FullName.GetHashCode();

        public static readonly TypeDefinitionComparer Instance = new();
    }

    private TypeDefinition[] LoadPublicTypesFrom(string[] referenceAssemblyPaths)
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
                return Array.Empty<AssemblyDefinition>();
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
                type.FullName == "C.module").
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
                return methods.Concat(fields).ToArray();
            }).
            // Ignored trailing existence symbol names.
            Distinct(MemberDefinitionNameComparer.Instance).
            ToDictionary(member => member.Name);
    }

    private void AssembleFromSource(
        Parser parser,
        TextReader reader,
        string relativeSourcePath)
    {
        parser.SetSourceFile(relativeSourcePath);

        var tokenizer = new Tokenizer();

        while (true)
        {
            var line = reader.ReadLine();
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
        string[] referenceAssemblyPaths,
        AssemblyTypes assemblyType,
        DebugSymbolTypes debugSymbolType,
        AssembleOptions options,
        Version version,
        string targetFrameworkMoniker,
        Action<Parser> runner)
    {
        var referenceTypes = this.LoadPublicTypesFrom(
            referenceAssemblyPaths);

        var cabiSpecificSymbols = this.AggregateCAbiSpecificSymbols(
            referenceTypes);

        //////////////////////////////////////////////////////////////

        var assemblyName = new AssemblyNameDefinition(
            Path.GetFileNameWithoutExtension(outputAssemblyPath),
            version);
        var assembly = AssemblyDefinition.CreateAssembly(
            assemblyName,
            Path.GetFileName(outputAssemblyPath),
            assemblyType switch
            {
                AssemblyTypes.Dll => ModuleKind.Dll,
                AssemblyTypes.WinExe => ModuleKind.Windows,
                _ => ModuleKind.Console
            });

        var module = assembly.MainModule;

        var cabiSpecificModuleType = new TypeDefinition(
            "C",
            "module",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed |
            TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            module.TypeSystem.Object);
        module.Types.Add(cabiSpecificModuleType);

        //////////////////////////////////////////////////////////////

        var produceExecutable =
            assemblyType != AssemblyTypes.Dll;

        var parser = new Parser(
            this.logger,
            module,
            cabiSpecificModuleType,
            cabiSpecificSymbols,
            referenceTypes,
            produceExecutable,
            debugSymbolType != DebugSymbolTypes.None);

        runner(parser);

        var allFinished = parser.Finish(
            options.HasFlag(AssembleOptions.ApplyOptimization));

        //////////////////////////////////////////////////////////////

        if (allFinished)
        {
            module.Write(
                outputAssemblyPath,
                new()
                {
                    DeterministicMvid =
                        options.HasFlag(AssembleOptions.Deterministic),
                    WriteSymbols =
                        debugSymbolType != DebugSymbolTypes.None,
                    SymbolWriterProvider = debugSymbolType switch
                    {
                        DebugSymbolTypes.None => null!,
                        DebugSymbolTypes.Embedded => new EmbeddedPortablePdbWriterProvider(),
                        DebugSymbolTypes.WindowsProprietary => new PdbWriterProvider(),
                        _ => new PortablePdbWriterProvider(),
                    },
                });
        }

        return allFinished;
    }

    public bool Assemble(
        string[] sourcePaths,
        string outputAssemblyPath,
        string[] referenceAssemblyPaths,
        AssemblyTypes assemblyType,
        DebugSymbolTypes debugSymbolType,
        AssembleOptions options,
        Version version,
        string targetFrameworkMoniker)
    {
        if (sourcePaths.Length == 0)
        {
            return false;
        }

        var sourceFullPaths = sourcePaths.
            Select(Path.GetFullPath).
            ToArray();

        var baseSourcePath = sourceFullPaths.Length == 1 ?
            Utilities.GetDirectoryPath(sourceFullPaths[0]) :           
            Path.Combine(sourceFullPaths.
                Select(path => path.Split(Path.DirectorySeparatorChar)).
                Aggregate((path0, path1) => path0.Intersect(path1).ToArray()));  // Intersect is stable?

        //////////////////////////////////////////////////////////////

        return this.Run(
            outputAssemblyPath,
            referenceAssemblyPaths,
            assemblyType,
            debugSymbolType,
            options,
            version,
            targetFrameworkMoniker,
            parser =>
            {
                foreach (var sourceFullPath in sourceFullPaths)
                {
                    using var fs = new FileStream(
                        sourceFullPath,
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    var reader = new StreamReader(
                        fs,
                        true);

                    var relativeSourcePath = sourceFullPath.
                        Substring(baseSourcePath.Length + 1);

                    this.AssembleFromSource(
                        parser,
                        reader,
                        relativeSourcePath);
                }
            });
    }
}
