/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using chibias.Parsing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace chibias;

public readonly struct SourceCodeItem
{
    public readonly TextReader Reader;
    public readonly string SourcePathDebuggerHint;

    public SourceCodeItem(
        TextReader reader, string sourcePathDebuggerHint)
    {
        this.Reader = reader;
        this.SourcePathDebuggerHint = sourcePathDebuggerHint;
    }

    public override string ToString() =>
        this.SourcePathDebuggerHint ?? "(null)";
}

public sealed class Assembler
{
    private static readonly string runtimeConfigJsonTemplate =
        new StreamReader(typeof(Assembler).Assembly.GetManifestResourceStream(
            "chibias.Internal.runtimeconfig.json")!).
        ReadToEnd();

    private readonly ILogger logger;
    private readonly DefaultAssemblyResolver assemblyResolver;
    private readonly ReaderParameters readerParameters;

    public Assembler(
        ILogger logger,
        params string[] referenceAssemblyBasePaths)
    {
        this.logger = logger;
        this.assemblyResolver = new DefaultAssemblyResolver();
        this.readerParameters = new(ReadingMode.Immediate)
        {
            InMemory = true,
            ReadSymbols = false,
            ReadWrite = false,
            ThrowIfSymbolsAreNotMatching = false,
            AssemblyResolver = this.assemblyResolver,
        };

        foreach (var basePath in referenceAssemblyBasePaths)
        {
            this.assemblyResolver.AddSearchDirectory(basePath);
        }
    }

    private TypeDefinitionCache LoadPublicTypesFrom(
        string[] referenceAssemblyPaths)
    {
        var assemblies = referenceAssemblyPaths.
            Distinct().
            Collect(path =>
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        this.logger.Warning(
                            $"Unable to find reference assembly: {path}");
                        return null;
                    }

                    var assembly = AssemblyDefinition.ReadAssembly(path, this.readerParameters);
                    this.logger.Information(
                        $"Read reference assembly: {path}");
                    return assembly;
                }
                catch (Exception ex)
                {
                    this.logger.Warning(
                        $"Unable to read reference assembly: {path}, {ex.GetType().FullName}: {ex.Message}");
                    return null;
                }
            })
#if DEBUG
            .ToArray()
#endif
            ;

        static IEnumerable<TypeDefinition> IterateTypesDescendants(TypeDefinition type)
        {
            yield return type;
            foreach (var nestedType in type.NestedTypes)
            {
                if (nestedType.IsNestedPublic &&
                    (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum) &&
                    type.GenericParameters.Count == 0)
                {
                    foreach (var childType in IterateTypesDescendants(nestedType))
                    {
                        yield return childType;
                    }
                }
            }
        }

        var types = assemblies.
            SelectMany(assembly => assembly.Modules).
            SelectMany(module => module.Types).
            Where(type =>
                type.IsPublic &&
                (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum) &&
                type.GenericParameters.Count == 0).
            SelectMany(IterateTypesDescendants).
            Distinct(TypeDefinitionComparer.Instance).
            Collect(type => type?.Resolve())
#if DEBUG
            .ToArray()
#endif
            ;

        return new(this.logger, types);
    }

    private MemberDictionary<MemberReference> AggregateCAbiSpecificSymbols(
        TypeDefinitionCache referenceTypes)
    {
        var methods = referenceTypes.
            Where(type =>
                type.IsPublic && type.IsClass && type.IsAbstract && type.IsSealed &&
                type.Namespace == "C" && type.Name == "text").
            SelectMany(type => type.Methods.
                Where(method => method.IsPublic && method.IsStatic && !method.HasGenericParameters)).
            Cast<MemberReference>();

        var fields = referenceTypes.
            Where(type =>
                type.IsPublic && type.IsClass && type.IsAbstract && type.IsSealed &&
                type.Namespace == "C" && (type.Name == "data" || type.Name == "rdata")).
            SelectMany(type => type.Fields.
                Where(field => field.IsPublic && field.IsStatic));

        var types = referenceTypes.
            Where(type =>
                type.IsPublic && type.IsValueType &&
                type.Namespace == "C.type");

        return new(this.logger, methods.Concat(fields).Concat(types));
    }

    private void AssembleFromSource(
        Parser parser,
        string? baseSourcePath,
        string sourcePathDebuggerHint,
        TextReader sourceCodeReader)
    {
        parser.BeginNewCilSourceCode(
            baseSourcePath,
            sourcePathDebuggerHint,
            true);

        var tokenizer = new Tokenizer();

        var tokenizeLap = new List<TimeSpan>();
        var parseLap = new List<TimeSpan>();
        var sw = Stopwatch.StartNew();

        while (true)
        {
            var line = sourceCodeReader.ReadLine();
            if (line == null)
            {
                break;
            }

            var lap0 = sw.Elapsed;
            var tokens = tokenizer.TokenizeLine(line);

            var lap1 = sw.Elapsed;
            parser.Parse(tokens);

            var lap2 = sw.Elapsed;

            tokenizeLap.Add(lap1 - lap0);
            parseLap.Add(lap2 - lap1);
        }

        var tokenizeTotal = tokenizeLap.Aggregate((t1, t2) => t1 + t2);
        var tokenizeAverage = TimeSpan.FromTicks(tokenizeTotal.Ticks / tokenizeLap.Count);
        var parseTotal = parseLap.Aggregate((t1, t2) => t1 + t2);
        var parseAverage = TimeSpan.FromTicks(parseTotal.Ticks / parseLap.Count);

        this.logger.Trace($"Stat: {sourcePathDebuggerHint}: Tokenize: Total={tokenizeTotal}, Average={tokenizeAverage}, Count={tokenizeLap.Count}");
        this.logger.Trace($"Stat: {sourcePathDebuggerHint}: Parse: Total={parseTotal}, Average={parseAverage}, Count={parseLap.Count}");
    }

    private bool Run(
        string outputAssemblyPath,
        AssemblerOptions options,
        Func<Parser, bool> runner)
    {
        if (!TargetFramework.TryParse(
            options.TargetFrameworkMoniker,
            out var targetFramework))
        {
            this.logger.Error(
                $"Unknown target framework moniker: {options.TargetFrameworkMoniker}");
            return false;
        }

        this.logger.Information(
            $"Detected target framework: {targetFramework} [{options.TargetFrameworkMoniker}]");

        var outputAssemblyFullPath = Path.GetFullPath(outputAssemblyPath);

        //////////////////////////////////////////////////////////////

        var assemblyName = new AssemblyNameDefinition(
            Path.GetFileNameWithoutExtension(outputAssemblyFullPath),
            options.Version);

        using var assembly = AssemblyDefinition.CreateAssembly(
            assemblyName,
            Path.GetFileName(outputAssemblyFullPath),
            new ModuleParameters
            {
                Kind = options.AssemblyType switch
                {
                    AssemblyTypes.Dll => ModuleKind.Dll,
                    AssemblyTypes.WinExe => ModuleKind.Windows,
                    _ => ModuleKind.Console
                },
                Runtime = targetFramework.Runtime,
                AssemblyResolver = this.assemblyResolver,
                Architecture = options.TargetWindowsArchitecture switch
                {
                    TargetWindowsArchitectures.X64 => TargetArchitecture.AMD64,
                    TargetWindowsArchitectures.IA64 => TargetArchitecture.IA64,
                    TargetWindowsArchitectures.ARM => TargetArchitecture.ARM,
                    TargetWindowsArchitectures.ARMv7 => TargetArchitecture.ARMv7,
                    TargetWindowsArchitectures.ARM64 => TargetArchitecture.ARM64,
                    _ => TargetArchitecture.I386,
                },
            });

        var module = assembly.MainModule;

        module.Attributes = options.TargetWindowsArchitecture switch
        {
            TargetWindowsArchitectures.Preferred32Bit => ModuleAttributes.ILOnly | ModuleAttributes.Preferred32Bit,
            TargetWindowsArchitectures.X86 => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
            TargetWindowsArchitectures.ARM => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
            TargetWindowsArchitectures.ARMv7 => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
            _ => ModuleAttributes.ILOnly,
        };

        // https://github.com/jbevain/cecil/issues/646
        var coreLibraryReference = this.assemblyResolver.Resolve(
            targetFramework.CoreLibraryName,
            this.readerParameters);
        module.AssemblyReferences.Add(coreLibraryReference.Name);

        // The type system will be bring with explicitly assigned core library.
        Debug.Assert(module.TypeSystem.CoreLibrary == coreLibraryReference.Name);

        // Attention when different core library from target framework.
        if (coreLibraryReference.Name.FullName != targetFramework.CoreLibraryName.FullName)
        {
            this.logger.Warning(
                $"Core library mismatched: {coreLibraryReference.FullName}");
        }

        //////////////////////////////////////////////////////////////

        var referenceTypes = this.LoadPublicTypesFrom(
            options.ReferenceAssemblyPaths);

        var cabiSpecificSymbols = this.AggregateCAbiSpecificSymbols(
            referenceTypes);

        //////////////////////////////////////////////////////////////

        var produceExecutable =
            options.AssemblyType != AssemblyTypes.Dll;

        var parser = new Parser(
            this.logger,
            module,
            targetFramework,
            cabiSpecificSymbols,
            referenceTypes,
            produceExecutable,
            options.DebugSymbolType != DebugSymbolTypes.None);

        var allFinished = runner(parser);
        if (allFinished)
        {
            allFinished = parser.Finish(
                options.Options.HasFlag(AssembleOptions.ApplyOptimization),
                options.Options.HasFlag(AssembleOptions.DisableJITOptimization));
        }

        cabiSpecificSymbols.Finish();

        //////////////////////////////////////////////////////////////

        if (allFinished)
        {
            this.logger.Information(
                $"Writing: {Path.GetFileName(outputAssemblyFullPath)}");

            var outputAssemblyBasePath =
                Utilities.GetDirectoryPath(outputAssemblyFullPath);
            try
            {
                if (!Directory.Exists(outputAssemblyBasePath))
                {
                    Directory.CreateDirectory(outputAssemblyBasePath);
                }
            }
            catch
            {
            }

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

            if (options.ProduceRuntimeConfigurationIfRequired &&
                produceExecutable &&
                targetFramework.Identifier == TargetFrameworkIdentifiers.NETCoreApp)
            {
                var runtimeConfigJsonPath = Path.Combine(
                    Utilities.GetDirectoryPath(outputAssemblyFullPath),
                    Path.GetFileNameWithoutExtension(outputAssemblyFullPath) + ".runtimeconfig.json");

                this.logger.Information(
                    $"Writing: {Path.GetFileName(runtimeConfigJsonPath)}");

                using var tw = File.CreateText(runtimeConfigJsonPath);

                var sb = new StringBuilder(runtimeConfigJsonTemplate);
                sb.Replace("{tfm}", options.TargetFrameworkMoniker);
                if (targetFramework.Version.Build >= 0)
                {
                    sb.Replace("{tfv}", targetFramework.Version.ToString(3));
                }
                else
                {
                    sb.Replace("{tfv}", targetFramework.Version.ToString(2) + ".0");
                }

                tw.Write(sb.ToString());
                tw.Flush();
            }
        }

        return allFinished;
    }

    private bool Assemble(
        string outputAssemblyPath,
        AssemblerOptions options,
        string? baseSourcePath,
        SourceCodeItem[] sourceCodeItems)
    {
        if (sourceCodeItems.Length == 0)
        {
            return false;
        }

        return this.Run(
            outputAssemblyPath,
            options,
            parser =>
            {
                var allFinished = true;

                foreach (var sourceCodeItem in sourceCodeItems)
                {
                    this.logger.Information(
                        $"Assembling: {sourceCodeItem.SourcePathDebuggerHint}");

                    this.AssembleFromSource(
                        parser,
                        baseSourcePath,
                        sourceCodeItem.SourcePathDebuggerHint,
                        sourceCodeItem.Reader);
                }

                return allFinished;
            });
    }

    public bool Assemble(
        string outputAssemblyPath,
        AssemblerOptions options,
        params SourceCodeItem[] sourceCodeItems) =>
        this.Assemble(
            outputAssemblyPath,
            options,
            null,
            sourceCodeItems);

    public bool Assemble(
        string outputAssemblyPath,
        AssemblerOptions options,
        string sourcePathDebuggerHint,
        TextReader sourceCodeReader) =>
        this.Assemble(
            outputAssemblyPath,
            options,
            null,
            new[] { new SourceCodeItem(sourceCodeReader, sourcePathDebuggerHint) });

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
            Utilities.IntersectStrings(sourceFullPaths).
                TrimEnd(Path.DirectorySeparatorChar);

        this.logger.Information(
            $"Source code base path: {baseSourcePath}");

        //////////////////////////////////////////////////////////////

        var sourceCodeItems = sourceFullPaths.Select(sourceFullPath =>
        {
            if (sourceFullPath == "-")
            {
                return new SourceCodeItem(Console.In, "<stdin>");
            }
            else
            {
                if (!File.Exists(sourceFullPath))
                {
                    this.logger.Error(
                        $"Unable to find source code file: {sourceFullPath}");
                    return new SourceCodeItem();
                }

                try
                {
                    var fs = new FileStream(
                        sourceFullPath,
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    var reader = new StreamReader(
                        fs,
                        true);

                    var hint = sourceFullPath.
                        Substring(baseSourcePath.Length + 1);

                    return new SourceCodeItem(reader, hint);
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex,
                        $"Unable to open source code file: {sourceFullPath}");
                    return new SourceCodeItem();
                }
            }
        }).ToArray();

        try
        {
            if (sourceCodeItems.Any(sourceCodeItem => sourceCodeItem.Reader == null))
            {
                return false;
            }

            return this.Assemble(
                outputAssemblyPath,
                options,
                baseSourcePath,
                sourceCodeItems);
        }
        finally
        {
            foreach (var sourceCode in sourceCodeItems)
            {
                if (sourceCode.Reader != null &&
                    sourceCode.Reader != Console.In)
                {
                    sourceCode.Reader.Close();
                    sourceCode.Reader.Dispose();
                }
            }
        }
    }
}
