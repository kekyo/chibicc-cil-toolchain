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

    private TypeDefinition[] LoadPublicTypesFrom(string[] referenceAssemblyPaths) =>
        referenceAssemblyPaths.
            Select(this.assemblyResolver.ReadAssemblyFrom).
            SelectMany(assembly => assembly.Modules).
            SelectMany(module => module.Types).
            Where(type => type.IsPublic &&
                (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum) &&
                type.GenericParameters.Count == 0).
            ToArray();

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
        string baseSourcePath,
        string sourcePath)
    {
        using var fs = new FileStream(
            sourcePath,
            FileMode.Open, FileAccess.Read, FileShare.Read);
        var tr = new StreamReader(
            fs,
            true);

        var relativeSourcePath = sourcePath.
            Substring(baseSourcePath.Length + 1);
        parser.SetSourceFile(relativeSourcePath);

        var tokenizer = new Tokenizer();

        while (true)
        {
            var line = tr.ReadLine();
            if (line == null)
            {
                break;
            }

            var tokens = tokenizer.TokenizeLine(line);
            parser.Parse(tokens);
        }
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

        //////////////////////////////////////////////////////////////

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

        var baseSourcePath = sourceFullPaths.Length == 1 ?
            Utilities.GetDirectoryPath(sourceFullPaths[0]) :           
            Path.Combine(sourceFullPaths.
                Select(path => path.Split(Path.DirectorySeparatorChar)).
                Aggregate((path0, path1) => path0.Intersect(path1).ToArray()));  // Intersect is stable?

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

        foreach (var sourceFullPath in sourceFullPaths)
        {
            this.AssembleFromSource(
                parser,
                baseSourcePath,
                sourceFullPath);
        }

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
}
