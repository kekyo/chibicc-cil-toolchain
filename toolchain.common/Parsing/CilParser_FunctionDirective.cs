/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System.Linq;

namespace chibicc.toolchain.Parsing;

partial class CilParser
{
    private FunctionNode? ParseFunctionDirective(
        TokensIterator tokensIterator, Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing directive operands.");
            return null;
        }

        if (tokens.Length > 4)
        {
            this.OutputError(
                tokens[4],
                $"Too many operands: {tokens[4]}");
            return null;
        }

        var scopeToken = tokens[1];
        if (!TryLookupScopeDescriptorName(
            scopeToken,
            out var scope))
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var functionSignatureToken = tokens[2];
        if (!TypeParser.TryParse(functionSignatureToken, out var type) ||
            type is not FunctionSignatureNode fsn)
        {
            this.OutputError(
                functionSignatureToken,
                $"Invalid function signature: {functionSignatureToken}");
            return null;
        }
        
        var functionNameToken = tokens[3];
        if (functionNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                functionSignatureToken,
                $"Invalid function name: {functionNameToken}");
            return null;
        }

        var (localVariables, instructions) = this.ParseFunctionBody(
            tokensIterator);
        
        return new(
            new(functionNameToken),
            scope,
            fsn,
            localVariables,
            instructions,
            tokens[0]);
    }
}
