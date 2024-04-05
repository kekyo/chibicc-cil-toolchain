/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace chibicc.toolchain.Parsing;

partial class CilParser
{
    private StructureField[] ParseStructureFields(
        TokensIterator tokensIterator, bool isExplicit)
    {
        var structureFields = new List<StructureField>();

        while (tokensIterator.TryGetNext(out var tokens))
        {
            var token0 = tokens[0];
            switch (token0)
            {
                // Structure member:
                case (TokenTypes.Identity, _):
                    if (tokens.Length < 3)
                    {
                        this.OutputError(
                            tokens.Last(),
                            $"Missing member operand.");
                        continue;
                    }
                    if (tokens.Length > 4)
                    {
                        this.OutputError(
                            tokens[4],
                            $"Too many operands: {tokens[4]}");
                        continue;
                    }
                    var scopeToken = token0;
                    if (!TryLookupScopeDescriptorName(
                        scopeToken,
                        out var scope) ||
                        scope.Scope == Scopes.File)
                    {
                        this.OutputError(
                            scopeToken,
                            $"Invalid scope descriptor: {scopeToken}");
                        continue;
                    }
                    var memberTypeNameToken = tokens[1];
                    if (!TypeParser.TryParse(memberTypeNameToken, out var memberType) ||
                        memberType is FunctionSignatureNode)
                    {
                        this.OutputError(
                            memberTypeNameToken,
                            $"Invalid member type name: {memberTypeNameToken}");
                        continue;
                    }
                    var memberNameToken = tokens[2];
                    if (memberNameToken.Type != TokenTypes.Identity)
                    {
                        this.OutputError(
                            memberNameToken,
                            $"Invalid member name: {memberNameToken}");
                        continue;
                    }
                    int? memberOffset = null;
                    if (!isExplicit)
                    {
                        if (tokens.Length == 4)
                        {
                            this.OutputError(
                                tokens[3],
                                $"Could not apply member offset: {tokens[3]}");
                            continue;
                        }
                    }
                    else
                    {
                        if (tokens.Length == 3)
                        {
                            this.OutputError(
                                memberNameToken,
                                $"Missing member offset operand: {memberNameToken}");
                            continue;
                        }
                        if (!int.TryParse(
                            tokens[3].Text,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var offset) ||
                            offset < 0)
                        {
                            this.OutputError(
                                tokens[3],
                                $"Invalid member offset: {tokens[3].Text}");
                            continue;
                        }
                        memberOffset = offset;
                    }
                    structureFields.Add(new(
                        memberType,
                        new(memberNameToken),
                        memberOffset is { } mo ? new NumericNode(mo, tokens[3]) : null));
                    continue;
                
                // Unknown directive (to exit this context):
                case (TokenTypes.Directive, _):
                    break;
                
                // Invalid syntax (to continue this context):
                default:
                    this.OutputError(
                        token0,
                        $"Invalid syntax: {token0}");
                    continue;
            }

            // Push back the tokens and exit this context.
            tokensIterator.PushBack(tokens);
            break;
        }

        return structureFields.ToArray();
    }

    private StructureNode? ParseStructureDirective(
        TokensIterator tokensIterator, Token[] tokens)
    {
        if (tokens.Length < 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing structure operand.");
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

        bool? isExplicit = null;
        short? packSize = null;
        var aligningToken = tokens.ElementAtOrDefault(3);

        if (aligningToken != null)
        {
            if (aligningToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    aligningToken,
                    $"Invalid operand: {aligningToken}");
                return null;
            }
            
            var aligning = aligningToken.Text;
            if (aligning == "explicit")
            {
                isExplicit = true;
            }
            else if (short.TryParse(
                aligning,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ps1))
            {
                if (ps1 < 1)
                {
                    this.OutputError(
                        aligningToken,
                        $"Invalid pack size: {aligningToken}");
                    return null;
                }
                packSize = ps1;
            }
            else
            {
                this.OutputError(
                    aligningToken,
                    $"Invalid operand: {aligningToken}");
                return null;
            }
        }

        var structureNameToken = tokens[2];
        if (structureNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                structureNameToken,
                $"Invalid structure name: {structureNameToken}");
            return null;
        }

        var structureFields = this.ParseStructureFields(
            tokensIterator, isExplicit ?? false);

        return new(
            new(structureNameToken),
            scope,
            isExplicit is { } ie ? new BooleanNode(ie, aligningToken!) : null,
            packSize is { } ps2 ? new NumericNode(ps2, aligningToken!) : null,
            structureFields.ToArray(),
            tokens[0]);
    }
}
