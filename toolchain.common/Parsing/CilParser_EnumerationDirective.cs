/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace chibicc.toolchain.Parsing;

partial class CilParser
{
    private EnumerationValueNode[] ParseEnumerationValues(
        TokensIterator tokensIterator,
        EnumerationMemberValueManipulator manipulator)
    {
        var enumerationValues = new List<EnumerationValueNode>();
        var currentValue = manipulator.GetInitialMemberValue();
        
        while (tokensIterator.TryGetNext(out var tokens))
        {
            var token0 = tokens[0];
            switch (token0)
            {
                // Enumeration value field:
                case (TokenTypes.Identity, _):
                    if (tokens.Length > 2)
                    {
                        this.OutputError(
                            tokens[2],
                            $"Too many operands: {tokens[2]}");
                        continue;
                    }
                    if (tokens.Length == 2)
                    {
                        var valueToken = tokens[1];
                        if (valueToken is not (TokenTypes.Identity, _) ||
                            !manipulator.TryParseMemberValue(valueToken, out currentValue))
                        {
                            this.OutputError(
                                tokens[2],
                                $"Invalid value: {valueToken}");
                            continue;
                        }
                        enumerationValues.Add(new(
                            new(token0),
                            new(currentValue, valueToken)));
                    }
                    else
                    {
                        enumerationValues.Add(new(
                            new(token0),
                            new(currentValue, token0)));
                        currentValue = manipulator.IncrementMemberValue(currentValue);
                    }
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

        return enumerationValues.ToArray();
    }
    
    private EnumerationNode? ParseEnumerationDirective(
        TokensIterator tokensIterator,
        Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing enumeration operand.");
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
            out var scope) ||
            scope.Scope is Scopes.__Module__)
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }
        
        var underlyingTypeNameToken = tokens[2];
        if (!TypeParser.TryParse(underlyingTypeNameToken, out var underlyingType) ||
            underlyingType is not TypeIdentityNode(var underlyingTypeName, _) ||
            !EnumerationMemberValueManipulator.TryGetInstance(underlyingTypeName, out var manipulator))
        {
            this.OutputError(
                underlyingTypeNameToken,
                $"Invalid enumeration underlying type: {underlyingTypeNameToken}");
            return null;
        }
        
        var enumerationNameToken = tokens[3];
        if (enumerationNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                enumerationNameToken,
                $"Invalid enumeration name: {enumerationNameToken}");
            return null;
        }

        var enumerationValues = this.ParseEnumerationValues(
            tokensIterator,
            manipulator);

        return new(
            new(enumerationNameToken),
            scope,
            underlyingType,
            enumerationValues,
            tokens[0]);
    }
}
