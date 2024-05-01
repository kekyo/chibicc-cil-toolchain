/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Parsing;
using chibicc.toolchain.Logging;
using chibicc.toolchain.Tokenizing;
using chibild.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace chibild.Generating;

internal sealed class ObjectFileInputFragment :
    ObjectInputFragment
{
    private readonly Dictionary<string, TypeDeclarationNode> types;
    private readonly HashSet<string> variableNames;
    private readonly HashSet<string> functionNames;

    private ObjectFileInputFragment(
        string baseInputPath,
        string relativePath,
        GlobalVariableNode[] variables,
        GlobalConstantNode[] constants,
        FunctionNode[] functions,
        InitializerNode[] initializers,
        EnumerationNode[] enumerations,
        StructureNode[] structures) :
        base(baseInputPath, relativePath)
    {
        this.GlobalVariables = variables;
        this.GlobalConstants = constants;
        this.Functions = functions;
        this.Initializers = initializers;
        this.Enumerations = enumerations;
        this.Structures = structures;

        this.types =
            enumerations.Cast<TypeDeclarationNode>().
            Concat(structures).
            DistinctBy(td => td.Name.Identity).
            ToDictionary(td => td.Name.Identity);
        this.variableNames =
            variables.Select(v => v.Name.Identity).
            Concat(constants.Select(c => c.Name.Identity)).
            Distinct().
            ToHashSet();
        this.functionNames =
            functions.Select(f => f.Name.Identity).
            Distinct().
            ToHashSet();
    }

    public override string ToString() =>
        $"Object: {this.ObjectPath}";

    //////////////////////////////////////////////////////////////

    public override GlobalVariableNode[] GlobalVariables { get; }

    public override GlobalConstantNode[] GlobalConstants { get; }

    public override FunctionNode[] Functions { get; }

    public override InitializerNode[] Initializers { get; }

    public override EnumerationNode[] Enumerations { get; }

    public override StructureNode[] Structures { get; }

    //////////////////////////////////////////////////////////////

    public override bool ContainsTypeAndSchedule(
        TypeNode type,
        out Scopes scope)
    {
        if (this.types.TryGetValue(type.TypeIdentity, out var td))
        {
            scope = td.Scope.Scope;
            return true;
        }
        scope = default;
        return false;
    }

    public override bool ContainsVariableAndSchedule(
        IdentityNode variable) =>
        this.variableNames.Contains(variable.Identity);

    public override bool ContainsFunctionAndSchedule(
        IdentityNode function,
        FunctionSignatureNode? signature) =>
        // Ignored the signature, because contains only CABI functions.
        this.functionNames.Contains(function.Identity);

    //////////////////////////////////////////////////////////////

    public static ObjectFileInputFragment Load(
        ILogger logger,
        string baseInputPath,
        string relativePath,
        TextReader tr,
        bool isLocationOriginSource)
    {
        logger.Information($"Loading: {relativePath}");

        var parser = new CilParser(logger);
        var declarations = parser.Parse(
            CilTokenizer.TokenizeAll(baseInputPath, relativePath, tr),
            isLocationOriginSource).
            ToArray();

        var variables = declarations.OfType<GlobalVariableNode>().ToArray();
        var constants = declarations.OfType<GlobalConstantNode>().ToArray();
        var functions = declarations.OfType<FunctionNode>().ToArray();
        var initializers = declarations.OfType<InitializerNode>().ToArray();
        var enumerations = declarations.OfType<EnumerationNode>().ToArray();
        var structures = declarations.OfType<StructureNode>().ToArray();

        return new(baseInputPath, relativePath,
            variables,
            constants,
            functions,
            initializers,
            enumerations,
            structures);
    }
}
