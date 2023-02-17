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
                    var assembly = AssemblyDefinition.ReadAssembly(path, this.readerParameters);
                    this.logger.Information(
                        $"Read reference assembly: {path}");
                    return assembly;
                }
                catch (Exception ex)
                {
                    this.logger.Warning(
                        $"Unable read reference assembly: {path}, {ex.GetType().FullName}: {ex.Message}");
                    return null;
                }
            })
#if DEBUG
            .ToArray()
#endif
            ;

        var types = assemblies.
            SelectMany(assembly => assembly.Modules).
            SelectMany(module => module.Types).
            Distinct(TypeDefinitionComparer.Instance).
            Collect(type => type?.Resolve()).
            Where(type =>
                type.IsPublic &&
                (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum) &&
                type.GenericParameters.Count == 0)
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
                type.Namespace == "C" && type.Name == "data").
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
            sourcePathDebuggerHint);

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
        Action<Parser> runner)
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

        runner(parser);

        var allFinished = parser.Finish(
            options.Options.HasFlag(AssembleOptions.ApplyOptimization));

        cabiSpecificSymbols.Finish();

        //////////////////////////////////////////////////////////////

        if (allFinished)
        {
            this.logger.Information(
                $"Writing: {Path.GetFileName(outputAssemblyFullPath)}");

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
                this.logger.Information(
                    $"Assembling: {sourcePathDebuggerHint}");

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
            Utilities.IntersectStrings(sourceFullPaths).
                TrimEnd(Path.DirectorySeparatorChar);

        this.logger.Information(
            $"Source code base path: {baseSourcePath}");

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

                        this.logger.Information(
                            $"Assembling: {sourcePathDebuggerHint}");

                        this.AssembleFromSource(
                            parser,
                            baseSourcePath,
                            sourcePathDebuggerHint,
                            reader);
                    }
                    else
                    {
                        this.logger.Information(
                            "Assembling: <stdin>");

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
