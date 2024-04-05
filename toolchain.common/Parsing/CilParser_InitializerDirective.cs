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
    private InitializerNode? ParseInitializerDirective(
        TokensIterator tokensIterator,
        Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing directive operands.");
            return null;
        }

        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operands: {tokens[2]}");
            return null;
        }

        var scopeToken = tokens[1];
        if (!TryLookupScopeDescriptorName(
            scopeToken,
            out var scope) ||
            scope.Scope == Scopes.Public)
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var (localVariables, instructions) = this.ParseFunctionBody(
            tokensIterator);

        return new InitializerNode(
            scope,
            localVariables,
            instructions,
            tokens[0]);
    }
}
