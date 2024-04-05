/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Archiving;
using chibicc.toolchain.Logging;
using chibicc.toolchain.Parsing;
using chibicc.toolchain.Tokenizing;
using chibild.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace chibild;

public sealed class Linker
{
    private readonly ILogger logger;

    public Linker(ILogger logger) =>
        this.logger = logger;

    private TypeDefinitionCache LoadPublicTypesFrom(
        AssemblyDefinition? injectTargetAssembly,
        string[] libraryReferenceBasePaths,
        ILibraryReference[] libraryReferences,
        ReaderParameters readerParameters)
    {
        var assemblies = (injectTargetAssembly != null ?
            new[] { injectTargetAssembly! } : new AssemblyDefinition[0]).
            Concat(
                libraryReferences.
                Distinct().
                Collect(lr =>
                {
                    try
                    {
                        string path;
                        switch (lr)
                        {
                            case LibraryNameReference(var name):
                                if (libraryReferenceBasePaths.
                                    SelectMany(basePath =>
                                        new[] { $"lib{name}.a", $"lib{name}.dll", $"{name}.a", $"{name}.dll" }.
                                        Select(n => Path.Combine(basePath, n))).
                                    Where(File.Exists).
                                    FirstOrDefault() is not { } p1)
                                {
                                    this.logger.Warning(
                                        $"Unable to find the library: {lr}");
                                    return null;
                                }
                                path = p1;
                                break;
                            case LibraryPathReference(var p2):
                                if (!File.Exists(p2))
                                {
                                    this.logger.Warning(
                                        $"Unable to find the library: {lr}");
                                    return null;
                                }
                                path = p2;
                                break;
                            default:
                                return null;
                        }

                        if (Path.GetExtension(path) == ".a")
                        {
                            // TODO:
                            return null;
                        }
                        else
                        {
                            var assembly = AssemblyDefinition.ReadAssembly(path, readerParameters);
                            this.logger.Information(
                                $"Read reference assembly: {path}");
                            return assembly;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warning(
                            $"Unable to read the library: {lr}, {ex.GetType().FullName}: {ex.Message}");
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

    private bool InternalLink(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        string? baseSourcePath,
        IInputFileItem[] inputFileItems)
    {
        // Avoid nothing object files.
        if (inputFileItems.Length == 0)
        {
            return false;
        }
        
        //////////////////////////////////////////////////////////////

        using var assemblyResolver = new AssemblyResolver(
            this.logger, options.LibraryReferenceBasePaths);

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

        // Will be created a new assembly.
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
            options.LibraryReferenceBasePaths,
            options.LibraryReferences,
            readerParameters);

        // Aggregates CABI symbols.
        var cabiSpecificSymbols = this.AggregateCAbiSpecificSymbols(
            referenceTypes);

        //////////////////////////////////////////////////////////////

        // Parse CIL object files.
        var parsedResults = new DeclarationNode[inputFileItems.Length][];
        Parallel.ForEach(inputFileItems, (inputFileItem, _, index) =>
        {
            this.logger.Information(
                $"Parsing: {inputFileItem.ObjectFilePathDebuggerHint}");
                
            using var inputFileReader = inputFileItem.Open();

            var parser = new CilParser(this.logger);
            
            parsedResults[index] = parser.Parse(
                CilTokenizer.TokenizeAll(
                    baseSourcePath,
                    inputFileItem.ObjectFilePathDebuggerHint,
                    inputFileReader)).
                ToArray();
        });

        //////////////////////////////////////////////////////////////

        // Finalize parser.
        var allFinished = parser.Finish(
            options.CreationOptions?.TargetFramework,
            options.CreationOptions?.Options.HasFlag(AssembleOptions.DisableJITOptimization),
            options.ApplyOptimization);

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
                        DeterministicMvid = options.IsDeterministic,
                        WriteSymbols = options.DebugSymbolType != DebugSymbolTypes.None,
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

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        params IInputFileItem[] inputFileItems) =>
        this.InternalLink(
            outputAssemblyPath,
            options,
            injectToAssemblyPath,
            null,
            inputFileItems);

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        string objectFilePathDebuggerHint,
        TextReader inputFileReader) =>
        this.InternalLink(
            outputAssemblyPath,
            options,
            injectToAssemblyPath,
            null,
            new IInputFileItem[] { new InputTextReaderItem(() => inputFileReader, objectFilePathDebuggerHint) });

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        params string[] inputFilePaths)
    {
        if (inputFilePaths.Length == 0)
        {
            return false;
        }

        var inputFileFullPaths = inputFilePaths.
            Select(path => path != "-" ? Path.GetFullPath(path) : path).
            ToArray();

        var baseInputFilePath = inputFileFullPaths.Length == 1 ?
            Utilities.GetDirectoryPath(inputFileFullPaths[0]) :
            Utilities.IntersectStrings(inputFileFullPaths).
                TrimEnd(Path.DirectorySeparatorChar);

        this.logger.Information(
            $"Input file base path: {baseInputFilePath}");

        //////////////////////////////////////////////////////////////

        var inputFileItems = inputFileFullPaths.
            Select(inputFileFullPath =>
            {
                if (inputFileFullPath == "-")
                {
                    return (IInputFileItem)new InputStdInItem();
                }
                
                if (!File.Exists(inputFileFullPath))
                {
                    this.logger.Error(
                        $"Unable to find input file: {inputFileFullPath}");
                    return null;
                }

                if (File.Exists(inputFileFullPath))
                {
                    var hint = inputFileFullPath.
                        Substring(baseInputFilePath.Length + 1);
                    return new InputObjectFileItem(inputFileFullPath, hint);
                }
                else
                {
                    this.logger.Error(
                        $"Unable to open object file: {inputFileFullPath}");
                    return null;
                }
            }).
            ToArray();

        if (inputFileItems.Any(inputFileItem => inputFileItem == null))
        {
            return false;
        }

        return this.InternalLink(
            outputAssemblyPath,
            options,
            injectToAssemblyPath,
            baseInputFilePath,
            inputFileItems!);
    }
}
