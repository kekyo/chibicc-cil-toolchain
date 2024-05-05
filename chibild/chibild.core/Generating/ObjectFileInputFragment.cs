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
    private readonly Dictionary<string, VariableDeclarationNode> variables;
    private readonly Dictionary<string, FunctionDeclarationNode> functions;

    private ObjectFileInputFragment(
        string baseInputPath,
        string relativePath,
        GlobalVariableNode[] variables,
        GlobalConstantNode[] constants,
        FunctionDeclarationNode[] functions,
        InitializerDeclarationNode[] initializers,
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
        this.variables =
            variables.Cast<VariableDeclarationNode>().
            Concat(constants).
            DistinctBy(vd => vd.Name.Identity).
            ToDictionary(vd => vd.Name.Identity);
        this.functions =
            functions.
            DistinctBy(f => f.Name.Identity).
            ToDictionary(f => f.Name.Identity);
    }

    public override string ToString() =>
        $"Object: {this.ObjectPath}";

    //////////////////////////////////////////////////////////////

    public override GlobalVariableNode[] GlobalVariables { get; }

    public override GlobalConstantNode[] GlobalConstants { get; }

    public override FunctionDeclarationNode[] Functions { get; }

    public override InitializerDeclarationNode[] Initializers { get; }

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
        IdentityNode variable,
        out Scopes scope)
    {
        if (this.variables.TryGetValue(variable.Identity, out var vd))
        {
            scope = vd.Scope.Scope;
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
        if (this.functions.TryGetValue(function.Identity, out var f))
        {
            scope = f.Scope.Scope;
            return true;
        }
        scope = default;
        return false;
    }

    //////////////////////////////////////////////////////////////

    public static bool TryLoad(
        ILogger logger,
        string baseInputPath,
        string relativePath,
        TextReader tr,
        bool isLocationOriginSource,
        out ObjectFileInputFragment fragment)
    {
        logger.Information($"Loading: {relativePath}");

        var parser = new CilParser(logger);
        var declarations = parser.Parse(
            CilTokenizer.TokenizeAll(baseInputPath, relativePath, tr),
            isLocationOriginSource).
            ToArray();

        var variables = declarations.OfType<GlobalVariableNode>().ToArray();
        var constants = declarations.OfType<GlobalConstantNode>().ToArray();
        var functions = declarations.OfType<FunctionDeclarationNode>().ToArray();
        var initializers = declarations.OfType<InitializerDeclarationNode>().ToArray();
        var enumerations = declarations.OfType<EnumerationNode>().ToArray();
        var structures = declarations.OfType<StructureNode>().ToArray();

        fragment = new(baseInputPath, relativePath,
            variables,
            constants,
            functions,
            initializers,
            enumerations,
            structures);
        return !parser.CaughtError;
    }
}
