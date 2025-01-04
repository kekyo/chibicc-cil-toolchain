/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using chibicc.toolchain.Archiving;
using chibicc.toolchain.Parsing;
using chibicc.toolchain.Logging;
using chibicc.toolchain.Tokenizing;
using chibicc.toolchain.Internal;
using chibild.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using chibicc.toolchain.IO;

namespace chibild.Generating;

internal sealed class ArchivedObjectInputFragment :
    ObjectInputFragment
{
    // This class corresponds to the archived object file.
    // Initially, only the symbol table of the archive is loaded,
    // and the base class information is empty.
    // When a symbol is referenced, it is recorded with `isRequiredLoading`
    // and the information originally needed for the base class is
    // loaded from the archive with `LoadObjectIfRequired()`.

    private enum RequiredStates
    {
        Ignore,
        Required,
        Loaded,
    }

    private readonly string archivedObjectName;

    private readonly Dictionary<string, Symbol> typeSymbols;
    private readonly Dictionary<string, Symbol> variableSymbols;
    private readonly Dictionary<string, Symbol> functionSymbols;

    private GlobalVariableNode[] globalVariables = CommonUtilities.Empty<GlobalVariableNode>();
    private GlobalConstantNode[] globalConstants = CommonUtilities.Empty<GlobalConstantNode>();
    private FunctionDeclarationNode[] functions = CommonUtilities.Empty<FunctionDeclarationNode>();
    private InitializerDeclarationNode[] initializers = CommonUtilities.Empty<InitializerDeclarationNode>();
    private EnumerationNode[] enumerations = CommonUtilities.Empty<EnumerationNode>();
    private StructureNode[] structures = CommonUtilities.Empty<StructureNode>();

    private int requiredState = (int)RequiredStates.Ignore;

    private ArchivedObjectInputFragment(
        string baseInputPath,
        string relativePath,
        string archivedObjectName,
        Dictionary<string, Symbol> typeSymbols,
        Dictionary<string, Symbol> variableSymbols,
        Dictionary<string, Symbol> functionSymbols) :
        base(baseInputPath, relativePath)
    {
        this.archivedObjectName = archivedObjectName;
        this.ObjectName = Path.GetFileNameWithoutExtension(this.archivedObjectName);
        this.ObjectPath = $"{this.archivedObjectName}@{base.ObjectPath}";
        
        this.typeSymbols = typeSymbols;
        this.variableSymbols = variableSymbols;
        this.functionSymbols = functionSymbols;
    }

    public override string ObjectName { get; }

    public override string ObjectPath { get; }

    public bool IsRequired =>
        this.requiredState == (int)RequiredStates.Required;

    public override string ToString() =>
        $"ArchivedObject: {this.ObjectPath}, State={(RequiredStates)this.requiredState}";

    //////////////////////////////////////////////////////////////

    public override GlobalVariableNode[] GlobalVariables =>
        this.globalVariables;

    public override GlobalConstantNode[] GlobalConstants =>
        this.globalConstants;

    public override FunctionDeclarationNode[] Functions =>
        this.functions;

    public override InitializerDeclarationNode[] Initializers =>
        this.initializers;

    public override EnumerationNode[] Enumerations =>
        this.enumerations;

    public override StructureNode[] Structures =>
        this.structures;

    //////////////////////////////////////////////////////////////

    public override bool ContainsTypeAndSchedule(
        TypeNode type,
        out Scopes scope,
        out int? memberCount)
    {
        if (this.typeSymbols.TryGetValue(type.TypeIdentity, out var ts))
        {
            Interlocked.CompareExchange(
                ref this.requiredState,
                (int)RequiredStates.Required,
                (int)RequiredStates.Ignore);
            CommonUtilities.TryParseEnum(ts.Scope, out scope);
            memberCount = ts.MemberCount;
            return true;
        }
        scope = default;
        memberCount = null;
        return false;
    }

    public override bool ContainsVariableAndSchedule(
        IdentityNode variable,
        out Scopes scope)
    {
        if (this.variableSymbols.TryGetValue(variable.Identity, out var vs))
        {
            Interlocked.CompareExchange(
                ref this.requiredState,
                (int)RequiredStates.Required,
                (int)RequiredStates.Ignore);
            CommonUtilities.TryParseEnum(vs.Scope, out scope);
            return true;
        }
        scope = default;
        return false;
    }

    public override bool ContainsFunctionAndSchedule(
        IdentityNode function,
        FunctionSignatureNode? signature,
        out Scopes scope)
    {
        // Ignored the signature, because contains only CABI functions.
        if (this.functionSymbols.TryGetValue(function.Identity, out var fs))
        {
            Interlocked.CompareExchange(
                ref this.requiredState,
                (int)RequiredStates.Required,
                (int)RequiredStates.Ignore);
            CommonUtilities.TryParseEnum(fs.Scope, out scope);
            return true;
        }
        scope = default;
        return false;
    }

    //////////////////////////////////////////////////////////////

    public enum LoadObjectResults
    {
        Ignored,
        Loaded,
        CaughtError,
    }
    
    public LoadObjectResults LoadObjectIfRequired(
        ILogger logger,
        bool isLocationOriginSource)
    {
        if (Interlocked.CompareExchange(
            ref this.requiredState,
            (int)RequiredStates.Loaded,
            (int)RequiredStates.Required) == (int)RequiredStates.Required)
        {
            logger.Information($"Loading: {this.ObjectPath}");

            if (!ArchiverUtilities.TryOpenArchivedObject(
                Path.Combine(this.BaseInputPath, this.RelativePath),
                this.archivedObjectName,
                true,
                out var stream))
            {
                logger.Error(
                    $"Unable find an object on archive: ObjectName={this.archivedObjectName}, ArchiveFile={this.RelativePath}");
                return LoadObjectResults.CaughtError;
            }

            using var _s = stream;
            var tr = StreamUtilities.CreateTextReader(stream);

            var parser = new CilParser(logger);
            var declarations = parser.Parse(
                CilTokenizer.TokenizeAll(this.BaseInputPath, this.RelativePath, tr),
                isLocationOriginSource).
                ToArray();

            this.globalVariables = declarations.
                OfType<GlobalVariableNode>().
                ToArray();
            this.globalConstants = declarations.
                OfType<GlobalConstantNode>().
                ToArray();
            this.functions = declarations.
                OfType<FunctionDeclarationNode>().
                ToArray();
            this.initializers = declarations.
                OfType<InitializerDeclarationNode>().
                ToArray();
            this.enumerations = declarations.
                OfType<EnumerationNode>().
                OrderByDescending(e => e.Values.Length).
                DistinctBy(e => e.Name).
                ToArray();
            this.structures = declarations.
                OfType<StructureNode>().
                OrderByDescending(s => s.Fields.Length).
                DistinctBy(s => s.Name).
                ToArray();

            return parser.CaughtError ?
                LoadObjectResults.CaughtError :
                LoadObjectResults.Loaded;
        }
        return LoadObjectResults.Ignored;
    }

    public static ArchivedObjectInputFragment[] Load(
        ILogger logger,
        string baseInputPath,
        string relativePath)
    {
        logger.Information($"Loading symbol table: {relativePath}");

        var symbolLists = ArchiverUtilities.EnumerateSymbolListFromArchive(
            Path.Combine(baseInputPath, relativePath));
        
        return symbolLists.Select(symbolList =>
        {
            var symbols = symbolList.Symbols.
                GroupBy(symbol =>
                {
                    switch (symbol.Directive)
                    {
                        case "enumeration": return "type";
                        case "structure": return "type";
                        case "global": return "variable";
                        case "constant": return "variable";
                        case "function": return "function";
                        default:
                            logger.Warning($"Ignored invalid symbol table entry: {symbol.Directive}");
                            return "unknown";
                    }
                }).
                ToDictionary(
                    g => g.Key,
                    g => g.
                        // Takes largest member count.
                        OrderByDescending(symbol => symbol.MemberCount ?? 0).
                        DistinctBy(symbol => symbol.Name).
                        ToDictionary(symbol => symbol.Name));

            var empty = new Dictionary<string, Symbol>();
            
            return new ArchivedObjectInputFragment(
                baseInputPath,
                relativePath,
                symbolList.ObjectName,
                symbols.TryGetValue("type", out var types) ? types : empty,
                symbols.TryGetValue("variable", out var variableNames) ? variableNames : empty,
                symbols.TryGetValue("function", out var functionNames) ? functionNames : empty);
        }).
        ToArray();
    }
}
