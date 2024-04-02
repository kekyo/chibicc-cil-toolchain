/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using chibild.Internal;
using chibild.Parsing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using chibicc.toolchain.Logging;

namespace chibild;

public readonly struct ObjectFileItem
{
    public readonly TextReader Reader;
    public readonly string ObjectFilePathDebuggerHint;

    public ObjectFileItem(
        TextReader reader, string objectFilePathDebuggerHint)
    {
        this.Reader = reader;
        this.ObjectFilePathDebuggerHint = objectFilePathDebuggerHint;
    }

    public override string ToString() =>
        this.ObjectFilePathDebuggerHint ?? "(null)";
}

public sealed class Linker
{
    private readonly ILogger logger;

    public Linker(ILogger logger) =>
        this.logger = logger;

    private TypeDefinitionCache LoadPublicTypesFrom(
        AssemblyDefinition? injectTargetAssembly,
        string[] referenceAssemblyBasePaths,
        string[] referenceAssemblyNames,
        ReaderParameters readerParameters)
    {
        var assemblies = (injectTargetAssembly != null ?
            new[] { injectTargetAssembly! } : new AssemblyDefinition[0]).
            Concat(
                referenceAssemblyNames.
                Distinct().
                Collect(name =>
                {
                    try
                    {
                        if (referenceAssemblyBasePaths.
                            SelectMany(basePath => new[] { $"{name}.dll", $"lib{name}.dll" }.
                                Select(n => Path.Combine(basePath, n))).
                            Where(File.Exists).
                            FirstOrDefault() is not { } path)
                        {
                            this.logger.Warning(
                                $"Unable to find reference assembly: {name}");
                            return null;
                        }

                        var assembly = AssemblyDefinition.ReadAssembly(path, readerParameters);
                        this.logger.Information(
                            $"Read reference assembly: {path}");
                        return assembly;
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warning(
                            $"Unable to read reference assembly: {name}, {ex.GetType().FullName}: {ex.Message}");
                        return null;
                    }
                }))
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

        return new(this.logger,
            methods.Concat(fields).Concat(types).
            Distinct(MemberReferenceComparer.Instance));
    }

    private void LinkFromObjectFile(
        Parser parser,
        string? baseObjectFilePath,
        string objectFilePathDebuggerHint,
        TextReader objectFileReader)
    {
        parser.BeginNewCilSourceCode(
            baseObjectFilePath,
            objectFilePathDebuggerHint,
            true);

        var tokenizer = new Tokenizer();

        var tokenizeLap = new List<TimeSpan>();
        var parseLap = new List<TimeSpan>();
        var sw = Stopwatch.StartNew();

        while (true)
        {
            var line = objectFileReader.ReadLine();
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

        this.logger.Trace($"Stat: {objectFilePathDebuggerHint}: Tokenize: Total={tokenizeTotal}, Average={tokenizeAverage}, Count={tokenizeLap.Count}");
        this.logger.Trace($"Stat: {objectFilePathDebuggerHint}: Parse: Total={parseTotal}, Average={parseAverage}, Count={parseLap.Count}");
    }

    private bool Run(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        Func<Parser, bool> runner)
    {
        using var assemblyResolver = new AssemblyResolver(
            this.logger, options.ReferenceAssemblyBasePaths);

        var readerParameters = new ReaderParameters(ReadingMode.Immediate)
        {
            InMemory = true,
            ReadSymbols = false,
            ReadWrite = false,
            ThrowIfSymbolsAreNotMatching = false,
            AssemblyResolver = assemblyResolver,
        };

        var produceExecutable =
            options.CreationOptions?.AssemblyType != AssemblyTypes.Dll;
        
        var requireAppHost =
            produceExecutable &&
            options.CreationOptions?.TargetFramework.Identifier == TargetFrameworkIdentifiers.NETCoreApp &&
            options.CreationOptions?.AppHostTemplatePath is { } appHostTemplatePath &&
            !string.IsNullOrWhiteSpace(appHostTemplatePath);
        
        var outputAssemblyFullPath = Path.GetFullPath(outputAssemblyPath);
        var outputAssemblyCandidateFullPath = requireAppHost?
            Path.Combine(
                Utilities.GetDirectoryPath(outputAssemblyFullPath),
                Path.GetFileNameWithoutExtension(outputAssemblyFullPath) + ".dll") :
            outputAssemblyFullPath;

        //////////////////////////////////////////////////////////////

        AssemblyDefinition assembly;
        ModuleDefinition module;
        AssemblyDefinition? injectTargetAssembly = null;
        ModuleDefinition? injectTargetModule = null;

        // Will be create a new assembly.
        if (injectToAssemblyPath == null)
        {
            if (options.CreationOptions is not { } co1)
            {
                throw new ArgumentException("Required CreationOptions in assembly creating.");
            }
            
            var assemblyName = new AssemblyNameDefinition(
                Path.GetFileNameWithoutExtension(outputAssemblyCandidateFullPath),
                options.CreationOptions?.Version);

            assembly = AssemblyDefinition.CreateAssembly(
                assemblyName,
                Path.GetFileName(outputAssemblyCandidateFullPath),
                new ModuleParameters
                {
                    Kind = co1.AssemblyType switch
                    {
                        AssemblyTypes.Dll => ModuleKind.Dll,
                        AssemblyTypes.WinExe => ModuleKind.Windows,
                        _ => ModuleKind.Console
                    },
                    Runtime = co1.TargetFramework.Runtime,
                    AssemblyResolver = assemblyResolver,
                    Architecture = co1.TargetWindowsArchitecture switch
                    {
                        TargetWindowsArchitectures.X64 => TargetArchitecture.AMD64,
                        TargetWindowsArchitectures.IA64 => TargetArchitecture.IA64,
                        TargetWindowsArchitectures.ARM => TargetArchitecture.ARM,
                        TargetWindowsArchitectures.ARMv7 => TargetArchitecture.ARMv7,
                        TargetWindowsArchitectures.ARM64 => TargetArchitecture.ARM64,
                        _ => TargetArchitecture.I386,
                    },
                });

            module = assembly.MainModule;
            module.Attributes = co1.TargetWindowsArchitecture switch
            {
                TargetWindowsArchitectures.Preferred32Bit => ModuleAttributes.ILOnly | ModuleAttributes.Preferred32Bit,
                TargetWindowsArchitectures.X86 => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
                TargetWindowsArchitectures.ARM => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
                TargetWindowsArchitectures.ARMv7 => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
                _ => ModuleAttributes.ILOnly,
            };

            // https://github.com/jbevain/cecil/issues/646
            var coreLibraryReference = assemblyResolver.Resolve(
                co1.TargetFramework.CoreLibraryName,
                readerParameters);
            module.AssemblyReferences.Add(coreLibraryReference.Name);

            // Attention when different core library from target framework.
            if (coreLibraryReference.Name.FullName != co1.TargetFramework.CoreLibraryName.FullName)
            {
                this.logger.Warning(
                    $"Core library mismatched: {coreLibraryReference.FullName}");
            }

            // The type system will be bring with explicitly assigned core library.
            Debug.Assert(module.TypeSystem.CoreLibrary == coreLibraryReference.Name);
        }
        // Will be injected exist assembly.
        else
        {
            // Read it.
            injectTargetAssembly = assembly = assemblyResolver.ReadAssemblyFrom(
                injectToAssemblyPath);
            injectTargetModule = module = assembly.MainModule;
        }

        //////////////////////////////////////////////////////////////

        // Load all public CABI symbols from reference assemblies.
        var referenceTypes = this.LoadPublicTypesFrom(
            injectTargetAssembly,
            options.ReferenceAssemblyBasePaths,
            options.ReferenceAssemblyNames,
            readerParameters);

        // Aggregates CABI symbols.
        var cabiSpecificSymbols = this.AggregateCAbiSpecificSymbols(
            referenceTypes);

        //////////////////////////////////////////////////////////////

        // Parse CIL source files.
        var parser = new Parser(
            this.logger,
            module,
            cabiSpecificSymbols,
            referenceTypes,
            produceExecutable,
            options.DebugSymbolType != DebugSymbolTypes.None,
            injectTargetModule);

        var allFinished = runner(parser);

        // Finalize parser.
        if (allFinished)
        {
            allFinished = parser.Finish(
                options.CreationOptions?.TargetFramework,
                options.CreationOptions?.Options.HasFlag(AssembleOptions.DisableJITOptimization),
                options.ApplyOptimization);
        }

        cabiSpecificSymbols.Finish();

        //////////////////////////////////////////////////////////////

        // When finishes:
        if (allFinished)
        {
            this.logger.Information(
                $"Writing: {Path.GetFileName(outputAssemblyCandidateFullPath)}{(options.IsDryRun ? " (dryrun)" : "")}");

            if (options.IsDryRun)
            {
                return allFinished;
            }
            
            var outputAssemblyBasePath =
                Utilities.GetDirectoryPath(outputAssemblyCandidateFullPath);
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

            // Backup original assembly.
            string? backupFilePath = null;
            if (File.Exists(outputAssemblyCandidateFullPath))
            {
                backupFilePath = outputAssemblyCandidateFullPath + ".bak";
                File.Move(outputAssemblyCandidateFullPath, backupFilePath);
            }

            try
            {
                // Write a new assembly (derived metadatas when give injection target assembly)
                module.Write(
                    outputAssemblyCandidateFullPath,
                    new()
                    {
                        DeterministicMvid =
                            options.IsDeterministic,
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
            catch
            {
                // Recover backup assembly.
                try
                {
                    if (File.Exists(outputAssemblyCandidateFullPath))
                    {
                        File.Delete(outputAssemblyCandidateFullPath);
                    }
                    if (backupFilePath != null)
                    {
                        File.Move(backupFilePath, outputAssemblyCandidateFullPath);
                    }
                }
                catch
                {
                }
                throw;
            }

            // Completion deletes backup assembly.
            File.Delete(outputAssemblyCandidateFullPath + ".bak");

            // When creates a new assembly:
            if (options.CreationOptions is { } co2)
            {
                // .NET Core specialization:
                if (produceExecutable &&
                    co2.TargetFramework.Identifier == TargetFrameworkIdentifiers.NETCoreApp)
                {
                    // Writes runtime configuration file.
                    if (co2.RuntimeConfiguration != RuntimeConfigurationOptions.Omit)
                    {
                        NetCoreWriter.WriteRuntimeConfiguration(
                            this.logger,
                            outputAssemblyCandidateFullPath,
                            options);
                    }

                    // Writes AppHost bootstrapper.
                    if (co2.AppHostTemplatePath is { } appHostTemplatePath3 &&
                        !string.IsNullOrWhiteSpace(appHostTemplatePath3))
                    {
                        NetCoreWriter.WriteAppHost(
                            this.logger,
                            outputAssemblyFullPath,
                            outputAssemblyCandidateFullPath,
                            Path.GetFullPath(appHostTemplatePath3),
                            options);
                    }
                }
            }
        }

        return allFinished;
    }

    private bool InternalLink(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        string? baseSourcePath,
        ObjectFileItem[] objectFileItems)
    {
        if (objectFileItems.Length == 0)
        {
            return false;
        }

        return this.Run(
            outputAssemblyPath,
            options,
            injectToAssemblyPath,
            parser =>
            {
                var allFinished = true;

                foreach (var objectFileItem in objectFileItems)
                {
                    this.logger.Information(
                        $"Linking: {objectFileItem.ObjectFilePathDebuggerHint}");

                    this.LinkFromObjectFile(
                        parser,
                        baseSourcePath,
                        objectFileItem.ObjectFilePathDebuggerHint,
                        objectFileItem.Reader);
                }

                return allFinished;
            });
    }

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        params ObjectFileItem[] objectFileItems) =>
        this.InternalLink(
            outputAssemblyPath,
            options,
            injectToAssemblyPath,
            null,
            objectFileItems);

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        string objectFilePathDebuggerHint,
        TextReader objectFileReader) =>
        this.InternalLink(
            outputAssemblyPath,
            options,
            injectToAssemblyPath,
            null,
            new[] { new ObjectFileItem(objectFileReader, objectFilePathDebuggerHint) });

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        params string[] objectPaths)
    {
        if (objectPaths.Length == 0)
        {
            return false;
        }

        var objectFullPaths = objectPaths.
            Select(path => path != "-" ? Path.GetFullPath(path) : path).
            ToArray();

        var baseObjectFilePath = objectFullPaths.Length == 1 ?
            Utilities.GetDirectoryPath(objectFullPaths[0]) :
            Utilities.IntersectStrings(objectFullPaths).
                TrimEnd(Path.DirectorySeparatorChar);

        this.logger.Information(
            $"Object file base path: {baseObjectFilePath}");

        //////////////////////////////////////////////////////////////

        var objectFileItems = objectFullPaths.Select(objectFullPath =>
        {
            if (objectFullPath == "-")
            {
                return new ObjectFileItem(Console.In, "<stdin>");
            }
            else
            {
                if (!File.Exists(objectFullPath))
                {
                    this.logger.Error(
                        $"Unable to find object file: {objectFullPath}");
                    return new ObjectFileItem();
                }

                try
                {
                    var fs = new FileStream(
                        objectFullPath,
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                    var reader = new StreamReader(
                        fs,
                        true);

                    var hint = objectFullPath.
                        Substring(baseObjectFilePath.Length + 1);

                    return new ObjectFileItem(reader, hint);
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex,
                        $"Unable to open object file: {objectFullPath}");
                    return new ObjectFileItem();
                }
            }
        }).ToArray();

        try
        {
            if (objectFileItems.Any(objectFileItem => objectFileItem.Reader == null))
            {
                return false;
            }

            return this.InternalLink(
                outputAssemblyPath,
                options,
                injectToAssemblyPath,
                baseObjectFilePath,
                objectFileItems);
        }
        finally
        {
            foreach (var objectFile in objectFileItems)
            {
                if (objectFile.Reader != null &&
                    objectFile.Reader != Console.In)
                {
                    objectFile.Reader.Close();
                    objectFile.Reader.Dispose();
                }
            }
        }
    }
}
