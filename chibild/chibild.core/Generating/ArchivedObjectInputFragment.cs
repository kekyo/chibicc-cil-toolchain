/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

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

    private readonly string archivedObjectName;

    private readonly Dictionary<string, Symbol> typeSymbols;
    private readonly Dictionary<string, Symbol> variableSymbols;
    private readonly Dictionary<string, Symbol> functionSymbols;

    private GlobalVariableNode[] globalVariables = Utilities.Empty<GlobalVariableNode>();
    private GlobalConstantNode[] globalConstants = Utilities.Empty<GlobalConstantNode>();
    private FunctionNode[] functions = Utilities.Empty<FunctionNode>();
    private InitializerNode[] initializers = Utilities.Empty<InitializerNode>();
    private EnumerationNode[] enumerations = Utilities.Empty<EnumerationNode>();
    private StructureNode[] structures = Utilities.Empty<StructureNode>();

    private bool isRequiredLoading;

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

    public override string ToString() =>
        $"ArchivedObject: {this.ObjectPath}";

    //////////////////////////////////////////////////////////////

    public override GlobalVariableNode[] GlobalVariables =>
        this.globalVariables;

    public override GlobalConstantNode[] GlobalConstants =>
        this.globalConstants;

    public override FunctionNode[] Functions =>
        this.functions;

    public override InitializerNode[] Initializers =>
        this.initializers;

    public override EnumerationNode[] Enumerations =>
        this.enumerations;

    public override StructureNode[] Structures =>
        this.structures;

    //////////////////////////////////////////////////////////////

    public override bool ContainsTypeAndSchedule(
        TypeNode type,
        out Scopes scope)
    {
        if (this.typeSymbols.TryGetValue(type.TypeIdentity, out var td))
        {
            this.isRequiredLoading = true;
            CommonUtilities.TryParseEnum(td.Scope, out scope);
            return true;
        }
        scope = default;
        return false;
    }

    public override bool ContainsVariableAndSchedule(
        IdentityNode variable)
    {
        if (this.variableSymbols.ContainsKey(variable.Identity))
        {
            this.isRequiredLoading = true;
            return true;
        }
        return false;
    }

    public override bool ContainsFunctionAndSchedule(
        IdentityNode function,
        FunctionSignatureNode? signature)
    {
        // Ignored the signature, because contains only CABI functions.
        if (this.functionSymbols.ContainsKey(function.Identity))
        {
            this.isRequiredLoading = true;
            return true;
        }
        return false;
    }

    //////////////////////////////////////////////////////////////

    public bool LoadObjectIfRequired(
        ILogger logger,
        bool isLocationOriginSource)
    {
        if (this.isRequiredLoading)
        {
            logger.Information($"Loading: {this.ObjectPath}");

            using var stream = ArchiverUtilities.OpenArchivedObject(
                Path.Combine(this.BaseInputPath, this.RelativePath),
                this.archivedObjectName);
            var tr = new StreamReader(stream, Encoding.UTF8, true);

            var parser = new CilParser(logger);
            var declarations = parser.Parse(
                CilTokenizer.TokenizeAll(this.BaseInputPath, this.RelativePath, tr),
                isLocationOriginSource).
                ToArray();

            this.globalVariables = declarations.OfType<GlobalVariableNode>().ToArray();
            this.globalConstants = declarations.OfType<GlobalConstantNode>().ToArray();
            this.functions = declarations.OfType<FunctionNode>().ToArray();
            this.initializers = declarations.OfType<InitializerNode>().ToArray();
            this.enumerations = declarations.OfType<EnumerationNode>().ToArray();
            this.structures = declarations.OfType<StructureNode>().ToArray();

            this.isRequiredLoading = false;
            return true;
        }
        return false;
    }

    public static ArchivedObjectInputFragment[] Load(
        ILogger logger,
        string baseInputPath,
        string relativePath)
    {
        logger.Information($"Loading symbol table: {relativePath}");

        var symbolLists = ArchiverUtilities.EnumerateSymbolTable(
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
                    g => g.DistinctBy(symbol => symbol.Name).ToDictionary(symbol => symbol.Name));

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
