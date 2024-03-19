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
using System.Runtime.InteropServices;
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
    private static readonly byte[] appBinaryPathPlaceholderSearchValue =
        Encoding.UTF8.GetBytes("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2");

    private readonly ILogger logger;

    public Assembler(ILogger logger) =>
        this.logger = logger;

    private TypeDefinitionCache LoadPublicTypesFrom(
        AssemblyDefinition? mergeOriginAssembly,
        string[] referenceAssemblyBasePaths,
        string[] referenceAssemblyNames,
        ReaderParameters readerParameters)
    {
        var assemblies = (mergeOriginAssembly != null ?
            new[] { mergeOriginAssembly! } : new AssemblyDefinition[0]).
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

    private static string GetRollForwardValue(RuntimeConfigurationOptions option) =>
        option switch
        {
            RuntimeConfigurationOptions.ProduceCoreCLRMajorRollForward => "major",
            RuntimeConfigurationOptions.ProduceCoreCLRMinorRollForward => "minor",
            RuntimeConfigurationOptions.ProduceCoreCLRFeatureRollForward => "feature",
            RuntimeConfigurationOptions.ProduceCoreCLRPatchRollForward => "patch",
            RuntimeConfigurationOptions.ProduceCoreCLRLatestMajorRollForward => "latestMajor",
            RuntimeConfigurationOptions.ProduceCoreCLRLatestMinorRollForward => "latestMinor",
            RuntimeConfigurationOptions.ProduceCoreCLRLatestFeatureRollForward => "latestFeature",
            RuntimeConfigurationOptions.ProduceCoreCLRDisableRollForward => "disable",
            _ => throw new ArgumentException(),
        };
    
    private void WriteRuntimeConfiguration(
        string outputAssemblyCandidateFullPath, AssemblerOptions options)
    {
        var co = options.CreationOptions;

        Debug.Assert(co != null);

        var runtimeConfigJsonPath = Path.Combine(
            Utilities.GetDirectoryPath(outputAssemblyCandidateFullPath),
            Path.GetFileNameWithoutExtension(outputAssemblyCandidateFullPath) + ".runtimeconfig.json");

        this.logger.Information(
            $"Writing: {Path.GetFileName(runtimeConfigJsonPath)}");

        using var tw = File.CreateText(runtimeConfigJsonPath);

        var sb = new StringBuilder(runtimeConfigJsonTemplate);
        sb.Replace("{tfm}", co!.TargetFramework.Moniker);
        if (co.RuntimeConfiguration ==
            RuntimeConfigurationOptions.ProduceCoreCLR)
        {
            sb.Replace("{rollForward}", "");
        }
        else
        {
            sb.Replace(
                "{rollForward}",
                $"\"rollForward\": \"{GetRollForwardValue(co.RuntimeConfiguration)}\",{Environment.NewLine}    ");
        }
        if (co.TargetFramework.Version.Build >= 0)
        {
            sb.Replace("{tfv}", co.TargetFramework.Version.ToString(3));
        }
        else
        {
            sb.Replace("{tfv}", co.TargetFramework.Version.ToString(2) + ".0");
        }

        tw.Write(sb.ToString());
        tw.Flush();
    }

    private void WriteAppHost(
        string outputAssemblyFullPath,
        string outputAssemblyPath,
        string appHostTemplateFullPath,
        AssemblerOptions options)
    {
        using var ms = new MemoryStream();
        using (var fs = new FileStream(
            appHostTemplateFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fs.CopyTo(ms);
        }
        ms.Position = 0;

        var outputAssemblyName = Path.GetFileName(outputAssemblyPath);
        var outputAssemblyNameBytes = Encoding.UTF8.GetBytes(outputAssemblyName);

        var isPEImage = PEUtils.IsPEImage(ms);
        var outputFullPath = Path.Combine(
            Utilities.GetDirectoryPath(outputAssemblyFullPath),
            Path.GetFileNameWithoutExtension(outputAssemblyFullPath) + (isPEImage ? ".exe" : ""));
        
        this.logger.Information(
            $"Writing AppHost: {Path.GetFileName(outputFullPath)}{(isPEImage ? " (PE format)" : "")}");

        if (Utilities.UpdateBytes(
            ms,
            appBinaryPathPlaceholderSearchValue,
            outputAssemblyNameBytes))
        {
            if (isPEImage && options.CreationOptions?.AssemblyType == AssemblyTypes.WinExe)
            {
                PEUtils.SetWindowsGraphicalUserInterfaceBit(ms);
            }

            using (var fs = new FileStream(
                       outputFullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                ms.CopyTo(fs);
                fs.Flush();
            }

            if (!Utilities.IsInWindows)
            {
                while (true)
                {
                    var r = Utilities.chmod(outputFullPath,
                        chmodFlags.S_IXOTH | chmodFlags.S_IROTH |
                        chmodFlags.S_IXGRP | chmodFlags.S_IRGRP |
                        chmodFlags.S_IXUSR | chmodFlags.S_IWUSR | chmodFlags.S_IRUSR);
                    if (r != -1)
                    {
                        break;
                    }
                    var errno = Marshal.GetLastWin32Error();
                    if (errno != Utilities.EINTR)
                    {
                        Marshal.ThrowExceptionForHR(errno);
                    }
                }
            }
        }
        else
        {
            this.logger.Error(
                $"Invalid AppHost template file: {appHostTemplateFullPath}");
        }
    }

    private bool Run(
        string outputAssemblyPath,
        AssemblerOptions options,
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
        AssemblyDefinition? mergeOriginAssembly = null;
        ModuleDefinition? mergeOriginModule = null;

        if (options.CreationOptions is { } co1)
        {
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
        else
        {
            // HACK: Avoid file locking.
            var ms = new MemoryStream();
            using (var fs = new FileStream(
                outputAssemblyCandidateFullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                fs.CopyTo(ms);
            }
            ms.Position = 0;

            mergeOriginAssembly = assembly = assemblyResolver.ReadAssemblyFrom(
                outputAssemblyCandidateFullPath);
            mergeOriginModule = module = assembly.MainModule;
        }

        //////////////////////////////////////////////////////////////

        var referenceTypes = this.LoadPublicTypesFrom(
            mergeOriginAssembly,
            options.ReferenceAssemblyBasePaths,
            options.ReferenceAssemblyNames,
            readerParameters);

        var cabiSpecificSymbols = this.AggregateCAbiSpecificSymbols(
            referenceTypes);

        //////////////////////////////////////////////////////////////

        var parser = new Parser(
            this.logger,
            module,
            cabiSpecificSymbols,
            referenceTypes,
            produceExecutable,
            options.DebugSymbolType != DebugSymbolTypes.None,
            mergeOriginModule);

        var allFinished = runner(parser);
        if (allFinished)
        {
            allFinished = parser.Finish(
                options.CreationOptions?.TargetFramework,
                options.CreationOptions?.Options.HasFlag(AssembleOptions.DisableJITOptimization),
                options.ApplyOptimization);
        }

        cabiSpecificSymbols.Finish();

        //////////////////////////////////////////////////////////////

        if (allFinished)
        {
            this.logger.Information(
                $"Writing: {Path.GetFileName(outputAssemblyCandidateFullPath)}");

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

            string? backupFilePath = null;
            if (File.Exists(outputAssemblyCandidateFullPath))
            {
                backupFilePath = outputAssemblyCandidateFullPath + ".bak";
                File.Move(outputAssemblyCandidateFullPath, backupFilePath);
            }

            try
            {
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

            File.Delete(outputAssemblyCandidateFullPath + ".bak");

            if (options.CreationOptions is { } co2)
            {
                if (produceExecutable &&
                    co2.TargetFramework.Identifier == TargetFrameworkIdentifiers.NETCoreApp)
                {
                    if (co2.RuntimeConfiguration != RuntimeConfigurationOptions.Omit)
                    {
                        this.WriteRuntimeConfiguration(
                            outputAssemblyCandidateFullPath,
                            options);
                    }

                    if (co2.AppHostTemplatePath is { } appHostTemplatePath3 &&
                        !string.IsNullOrWhiteSpace(appHostTemplatePath3))
                    {
                        this.WriteAppHost(
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
