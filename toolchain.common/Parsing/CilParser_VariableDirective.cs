/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using chibicc.toolchain.Tokenizing;
using System.Diagnostics;
using System.Linq;

namespace chibicc.toolchain.Parsing;

public sealed partial class CilParser
{
    private VariableDeclarationNode? ParseVariableDirective(
        Token[] tokens,
        bool isConstant)
    {
        var valueTypeDisplayName = isConstant ? "constant" : "global variable";
        
        // Constant directive requires initializing data.
        if (tokens.Length < (isConstant ? 5 : 4))
        {
            this.OutputError(
                tokens.Last(),
                $"Missing {valueTypeDisplayName} operand.");
            return null;
        }

        var scopeToken = tokens[1];
        if (!TryLookupScopeDescriptorName(
            scopeToken,
            out var scope) ||
            scope.Scope is Scopes.__Module__)
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var globalTypeNameToken = tokens[2];
        if (!TypeParser.TryParse(globalTypeNameToken, out var globalType) ||
            globalType is FunctionSignatureNode)
        {
            this.OutputError(
                globalTypeNameToken,
                $"Invalid {valueTypeDisplayName} type name: {globalTypeNameToken}");
            return null;
        }

        var valueNameToken = tokens[3];
        if (valueNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                valueNameToken,
                $"Invalid {valueTypeDisplayName} name: {valueNameToken}");
            return null;
        }

        InitializingDataNode? initializeData = null;
        if (tokens.Length >= 5)
        {
            initializeData = new(
                tokens.
                    Skip(4).
                    Select(token =>
                    {
                        if (token.Type != TokenTypes.Identity)
                        {
                            this.OutputError(
                                token,
                                $"Invalid data operand: {token}");
                            return (byte)0;
                        }
                        if (!CommonUtilities.TryParseUInt8(
                            token.Text,
                            out var value))
                        {
                            this.OutputError(
                                token,
                                $"Invalid data operand: {token}");
                            return (byte)0;
                        }
                        return value;
                    }).
                    ToArray(),
                tokens[4]);
        }
        
        Debug.Assert(!isConstant || initializeData != null);

        return isConstant ?
            new GlobalConstantNode(
                new(valueNameToken),
                scope,
                globalType,
                initializeData!,
                tokens[0]) :
            new GlobalVariableNode(
                new(valueNameToken),
                scope,
                globalType,
                initializeData,
                tokens[0]);
    }
}
