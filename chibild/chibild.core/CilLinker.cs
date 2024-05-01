/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.IO;
using chibicc.toolchain.Logging;
using chibild.Generating;
using chibild.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chibild;

public sealed class CilLinker
{
    // Basic strategy:
    // The `InputReference` contains information abstracting object input in the intended order
    // (or, if using the CLI, according to the sequence of command line options).
    // This includes archive files (*.a), which contain multiple objects (*.o)
    // in the archive distribution.
    // * Assembly references (*.dll) are loaded in `LoadInputReferences()`.
    // * Single object (*.o) is unconditionally parsed and code generated(`ConsumePhase1()`).
    // * A group of objects in an archive should not all be parsed immediately,
    //   but only when a symbol is referenced by another object.
    // * Since the archive contains a symbol table, only the symbol table
    //   should be loaded and the symbols checked.
    //   During code generation, if an object is found to use a symbol,
    //   the entire object should be scheduled for parsing and code generation and executed later.
    //   To achieve this, code generation takes n attempts(`ConsumePhase2()`).

    private readonly ILogger logger;

    public CilLinker(ILogger logger) =>
        this.logger = logger;

    //////////////////////////////////////////////////////////////

    private CachedAssemblyResolver CreateAssemblyResolver(
        ReadingMode readingMode,
        string[] libraryReferenceBasePaths) =>
        new(this.logger, readingMode, libraryReferenceBasePaths);

    //////////////////////////////////////////////////////////////

    private InputFragment[] LoadInputReferences(
        string baseInputPath,
        string[] libraryReferenceBasePaths,
        InputReference[] inputReferences,
        bool isLocationOriginSource)
    {
        // Load input files in parallelism.
        var loadedFragmentLists = new InputFragment[inputReferences.Length][];

#if DEBUG
        for (var index = 0; index < inputReferences.Length; index++)
        {
            var ir = inputReferences[index];
#else
        Parallel.ForEach(inputReferences,
            (ir, _, index) =>
            {
#endif
            switch (ir)
            {
                // Reader input:
                case ObjectReaderReference(var path, var reader):
                    using (var tr = reader())
                    {
                        loadedFragmentLists[index] = new[]
                        {
                            ObjectFileInputFragment.Load(
                                this.logger, "", path, tr,
                                isLocationOriginSource),
                        };
                    }
                    break;

                // Object file:
                case ObjectFilePathReference(var relativePath):
                    var objectFilePath = Path.Combine(baseInputPath, relativePath);
                    if (!File.Exists(objectFilePath))
                    {
                        this.logger.Error(
                            $"Unable to find the object file: {relativePath}");
                        break;
                    }
                    using (var fs = StreamUtilities.OpenStream(
                        Path.Combine(baseInputPath, relativePath), false))
                    {
                        var tr = new StreamReader(fs, Encoding.UTF8, true);
                        loadedFragmentLists[index] = new[]
                        {
                            ObjectFileInputFragment.Load(
                                this.logger, baseInputPath, relativePath, tr,
                                isLocationOriginSource),
                        };
                    }
                    break;

                // Archive library:
                case LibraryPathReference(var relativePath)
                    when Path.GetExtension(relativePath) == ".a":
                    var archiveFilePath = Path.Combine(baseInputPath, relativePath);
                    if (!File.Exists(archiveFilePath))
                    {
                        this.logger.Warning(
                            $"Unable to find the archive file: {relativePath}");
                        break;
                    }
                    // Multiple objects in an archive.
                    loadedFragmentLists[index] = ArchivedObjectInputFragment.Load(
                        this.logger, baseInputPath, relativePath);
                    break;

                // Asssembly:
                case LibraryPathReference(var relativePath):
                    var libraryFilePath = Path.Combine(baseInputPath, relativePath);
                    if (!File.Exists(libraryFilePath))
                    {
                        this.logger.Warning(
                            $"Unable to find the library: {relativePath}");
                        break;
                    }
                    loadedFragmentLists[index] = new[]
                    {
                        AssemblyInputFragment.Load(
                            this.logger,
                            baseInputPath,
                            relativePath,
                            // Create this assembly specific resolver,
                            // because shared resolver can not resolve on multi-threaded context.
                            // At the cost of having to load it again later in the primary assembly resolver.
                            this.CreateAssemblyResolver(
                                ReadingMode.Immediate,
                                libraryReferenceBasePaths)),
                    };
                    break;

                // Archive/Assembly by the name:
                case LibraryNameReference(var name):
                    if (libraryReferenceBasePaths.
                        SelectMany(basePath => new[]
                            { $"lib{name}.a", $"lib{name}.dll", $"{name}.a", $"{name}.dll" }.
                            Select(fileName => new { basePath, fileName })).
                        FirstOrDefault(entry => File.Exists(Path.Combine(entry.basePath, entry.fileName))) is not { } foundEntry)
                    {
                        this.logger.Warning(
                            $"Unable to find the library: -l{name}");
                        break;
                    }
                    if (Path.GetExtension(foundEntry.fileName) == ".a")
                    {
                        loadedFragmentLists[index] = ArchivedObjectInputFragment.Load(
                            this.logger, foundEntry.basePath, foundEntry.fileName);
                    }
                    else
                    {
                        loadedFragmentLists[index] = new[]
                        {
                            AssemblyInputFragment.Load(
                                this.logger,
                                foundEntry.basePath,
                                foundEntry.fileName,
                                // Create this assembly specific resolver,
                                // because shared resolver can not resolve on multi-threaded context.
                                // At the cost of having to load it again later in the primary assembly resolver.
                                this.CreateAssemblyResolver(
                                    ReadingMode.Immediate,
                                    libraryReferenceBasePaths)),
                        };
                    }
                    break;

                default:
                    throw new InvalidOperationException();
            }
#if DEBUG
        }
#else
            });
#endif

        return loadedFragmentLists.
            SelectMany(loadedFragments => loadedFragments).
            ToArray();
    }

    //////////////////////////////////////////////////////////////

    private AssemblyDefinition CreateNewAssembly(
        string outputAssemblyCandidateFullPath,
        LinkerOptions options,
        CachedAssemblyResolver assemblyResolver)
    {
        Debug.Assert(options.CreationOptions != null);

        var assemblyName = new AssemblyNameDefinition(
            Path.GetFileNameWithoutExtension(outputAssemblyCandidateFullPath),
            options.CreationOptions!.Version);

        var assembly = AssemblyDefinition.CreateAssembly(
            assemblyName,
            Path.GetFileName(outputAssemblyCandidateFullPath),
            new ModuleParameters
            {
                Kind = options.CreationOptions!.AssemblyType switch
                {
                    AssemblyTypes.Dll => ModuleKind.Dll,
                    AssemblyTypes.WinExe => ModuleKind.Windows,
                    _ => ModuleKind.Console
                },
                Runtime = options.CreationOptions!.TargetFramework.Runtime,
                AssemblyResolver = assemblyResolver,
                Architecture = options.CreationOptions!.TargetWindowsArchitecture switch
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
        module.Attributes = options.CreationOptions!.TargetWindowsArchitecture switch
        {
            TargetWindowsArchitectures.Preferred32Bit => ModuleAttributes.ILOnly | ModuleAttributes.Preferred32Bit,
            TargetWindowsArchitectures.X86 => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
            TargetWindowsArchitectures.ARM => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
            TargetWindowsArchitectures.ARMv7 => ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit,
            _ => ModuleAttributes.ILOnly,
        };

        // https://github.com/jbevain/cecil/issues/646
        var coreLibraryReference = assemblyResolver.Resolve(
            options.CreationOptions!.TargetFramework.CoreLibraryName);
        module.AssemblyReferences.Add(coreLibraryReference.Name);

        // Attention when different core library from target framework.
        if (coreLibraryReference.Name.FullName != options.CreationOptions!.TargetFramework.CoreLibraryName.FullName)
        {
            this.logger.Warning(
                $"Core library mismatched: {coreLibraryReference.FullName}");
        }

        // The type system will be bring with explicitly assigned core library.
        Debug.Assert(module.TypeSystem.CoreLibrary == coreLibraryReference.Name);

        return assembly;
    }

    //////////////////////////////////////////////////////////////

    public bool Link(
        string outputAssemblyPath,
        LinkerOptions options,
        string? injectToAssemblyPath,
        string baseInputPath,
        params InputReference[] inputReferences)
    {
        if (!inputReferences.OfType<ObjectInputReference>().
            Any())
        {
            return false;
        }

        //////////////////////////////////////////////////////////////

        // Define totally input references.
        var totalInputReferences =
            injectToAssemblyPath is { } injectPath ?
                inputReferences.Prepend(new LibraryPathReference(injectPath)).ToArray() :
                inputReferences;

        var produceDebuggingInformation =
            options.DebugSymbolType != DebugSymbolTypes.None;

        // Load from inputs.
        var loadedFragments = this.LoadInputReferences(
            baseInputPath,
            options.LibraryReferenceBasePaths,
            totalInputReferences,
            produceDebuggingInformation);

        //////////////////////////////////////////////////////////////

        var produceExecutable =
            options.CreationOptions?.AssemblyType != AssemblyTypes.Dll;
        var entryPointSymbol =
            produceExecutable ? options.CreationOptions?.EntryPointSymbol : null;
        var targetFramework =
            options.CreationOptions?.TargetFramework;
        var disableJITOptimization =
            options.CreationOptions?.Options.HasFlag(AssembleOptions.DisableJITOptimization);
        var applyOptimization =
            options.ApplyOptimization;
        
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

        // Construct primary (output) assembly.
        AssemblyDefinition primaryAssembly;
        
        // Will be injected exist (loaded) assembly.
        if (injectToAssemblyPath != null)
        {
            primaryAssembly = ((AssemblyInputFragment)loadedFragments[0]).Assembly;
        }
        // Will be created a new assembly.
        else
        {
            if (options.CreationOptions == null)
            {
                this.logger.Error("Required CreationOptions in assembly creating.");
                return false;
            }
            
            primaryAssembly = this.CreateNewAssembly(
                outputAssemblyCandidateFullPath,
                options,
                this.CreateAssemblyResolver(
                    ReadingMode.Deferred,
                    options.LibraryReferenceBasePaths));
        }

        var targetModule = primaryAssembly.MainModule;

        //////////////////////////////////////////////////////////////

        var codeGenerator = new CodeGenerator(
            this.logger,
            targetModule,
            produceDebuggingInformation);

        if (!codeGenerator.ConsumeInputs(
            loadedFragments,
            produceDebuggingInformation))
        {
            return false;
        }

        if (!codeGenerator.Emit(
            loadedFragments,
            applyOptimization,
            targetFramework,
            disableJITOptimization,
            entryPointSymbol))
        {
            return false;
        }

        //////////////////////////////////////////////////////////////

        this.logger.Information(
            $"Writing: {Path.GetFileName(outputAssemblyCandidateFullPath)}{(options.IsDryRun ? " (dryrun)" : "")}");

        if (options.IsDryRun)
        {
            return true;
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
            targetModule.Write(
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

        //////////////////////////////////////////////////////////////

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

        return true;
    }
}
