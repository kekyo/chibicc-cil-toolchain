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
using System.Globalization;
using System.Linq;

// System.Reflection.Emit declarations are used only referring OpCode metadata.
using System.Reflection.Emit;

namespace chibicc.toolchain.Parsing;

partial class CilParser
{
    private static readonly Dictionary<string, OpCode> opCodes =
        typeof(OpCodes).GetFields().
            Where(field =>
                field.IsPublic && field.IsStatic && field.IsInitOnly &&
                field.FieldType.FullName == "System.Reflection.Emit.OpCode").
            Select(field => (OpCode)field.GetValue(null)!).
            ToDictionary(
                opCode => opCode.Name!.Replace('_', '.'),
                StringComparer.OrdinalIgnoreCase);

    /////////////////////////////////////////////////////////////////////

    private Location? ParseLocationDirective(
        Token[] tokens)
    {
        if (tokens.Length < 6)
        {
            this.OutputError(
                tokens.Last(),
                "Missing location operand.");
            return null;
        }
        
        if (tokens.Length > 6)
        {
            this.OutputError(
                tokens[6],
                $"Too many operands: {tokens[6]}");
            return null;
        }

        var fileIdToken = tokens[1];
        
        if (!this.files.TryGetValue(fileIdToken.Text, out var file))
        {
            this.OutputError(
                fileIdToken,
                $"Unknown file ID: {fileIdToken}");
            return null;
        }

        var vs = tokens.
            Skip(2).
            Select(token => uint.TryParse(
                token.Text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var vi) ? vi : default(uint?)).
            Where(v => v.HasValue).
            Select(v => v!.Value).
            ToArray();
        if ((vs.Length != (tokens.Length - 2)) ||
            (vs[0] > vs[2]) ||
            (vs[1] >= vs[3]))
        {
            this.OutputError(
                tokens[2],
                $"Invalid operand: {tokens[2]}");
            return null;
        }
        
        return new(
            file, vs[0], vs[1], vs[2], vs[3]);
    }
    
    private bool ParseHiddenDirective(
        Token[] tokens)
    {
        if (tokens.Length > 1)
        {
            this.OutputError(
                tokens[1],
                $"Too many operands: {tokens[1]}");
            return false;
        }

        return true;
    }
    
    /////////////////////////////////////////////////////////////////////

    private LocalVariableNode? ParseLocalDirective(
        Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing local variable operand.");
            return null;
        }

        if (tokens.Length > 3)
        {
            this.OutputError(
                tokens[3],
                $"Too many operands: {tokens[3]}");
            return null;
        }

        var localTypeNameToken = tokens[1];

        if (localTypeNameToken.Type != TokenTypes.Identity ||
            !TypeParser.TryParse(localTypeNameToken, out var localType) ||
            localType is FunctionSignatureNode)
        {
            this.OutputError(
                localTypeNameToken,
                $"Invalid local variable type name: {localTypeNameToken}");
            return null;
        }

        if (tokens.Length == 3)
        {
            var localNameToken = tokens[2];
            
            if (localNameToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    localNameToken,
                    $"Invalid local variable name: {localNameToken}");
                return null;
            }

            return new(localType, new(localNameToken), tokens[0]);
        }
        else
        {
            return new(localType, null, tokens[0]);
        }
    }

    /////////////////////////////////////////////////////////////////////

    private Label? ParseLabel(
        Token[] tokens)
    {
        if (tokens.Length > 1)
        {
            this.OutputError(
                tokens[1],
                $"Too many operands: {tokens[1]}");
            return null;
        }

        var labelToken = tokens[0];
        
        return new(
            new(labelToken),
            this.currentLocation);
    }

    private Instruction? ParseIdentityInstruction(
        Token[] tokens,
        string displayName,
        Func<IdentityNode, IdentityNode, Location?, Instruction?> generator)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 3)
        {
            this.OutputError(
                tokens[3],
                $"Too many operand: {tokens[3]}");
            return null;
        }
                
        var identityToken = tokens[1];
        if (identityToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                identityToken,
                $"Invalid {displayName}: {identityToken}");
        }

        return generator(
            new(tokens[0]),
            new(identityToken),
            this.currentLocation);
    }

    private TypeInstruction? ParseTypeInstruction(
        Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 3)
        {
            this.OutputError(
                tokens[3],
                $"Too many operand: {tokens[3]}");
            return null;
        }
                
        var typeNameToken = tokens[1];
        if (typeNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                typeNameToken,
                $"Invalid type name: {typeNameToken}");
        }
        if (!TypeParser.TryParse(typeNameToken, out var type) ||
            type is FunctionSignatureNode)
        {
            this.OutputError(
                typeNameToken,
                $"Invalid type name: {typeNameToken}");
        }

        return new(
            new(tokens[0]),
            type,
            this.currentLocation);
    }

    private NumericValueInstruction? ParseNumericValueInstruction(
        Token[] tokens,
        Func<string, object?> numericParser)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operand: {tokens[2]}");
            return null;
        }
                
        var valueToken = tokens[1];
        if (valueToken.Type != TokenTypes.Identity ||
            numericParser(valueToken.Text) is not { } value)
        {
            this.OutputError(
                valueToken,
                $"Invalid numeric value: {valueToken}");
            return null;
        }

        return new(
            new(tokens[0]),
            new(value, valueToken),
            this.currentLocation);
    }

    private StringValueInstruction? ParseStringValueInstruction(
        Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operand: {tokens[2]}");
            return null;
        }
                
        var valueToken = tokens[1];
        if (valueToken.Type != TokenTypes.String)
        {
            this.OutputError(
                valueToken,
                $"Invalid string value: {valueToken}");
            return null;
        }

        return new(
            new(tokens[0]),
            new(valueToken),
            this.currentLocation);
    }

    private VariableInstruction? ParseVariableInstruction(
        Token[] tokens, bool isShort)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operand: {tokens[2]}");
            return null;
        }
                
        var variableIdentityToken = tokens[1];
        if (variableIdentityToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                variableIdentityToken,
                $"Invalid variable identity: {variableIdentityToken}");
            return null;
        }

        if (isShort && byte.TryParse(
            variableIdentityToken.Text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var u8))
        {
            return new VariableIndexInstruction(
                new(tokens[0]),
                new(u8, variableIdentityToken),
                this.currentLocation);
        }
        
        if (!isShort && uint.TryParse(
            variableIdentityToken.Text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var u32))
        {
            return new VariableIndexInstruction(
                new(tokens[0]),
                new(u32, variableIdentityToken),
                this.currentLocation);
        }
        
        return new VariableNameInstruction(
            new(tokens[0]),
            new(variableIdentityToken),
            this.currentLocation);
    }

    private CallInstruction? ParseCallInstruction(
        Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 3)
        {
            this.OutputError(
                tokens[3],
                $"Too many operand: {tokens[3]}");
            return null;
        }

        if (tokens.Length == 3)
        {
            var signatureToken = tokens[1];
            if (signatureToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    signatureToken,
                    $"Invalid function signature: {signatureToken}");
            }
            if (!TypeParser.TryParse(signatureToken, out var signature) ||
                signature is not FunctionSignatureNode fsn)
            {
                this.OutputError(
                    signatureToken,
                    $"Invalid function signature: {signatureToken}");
                return null;
            }

            var functionToken = tokens[2];
            if (functionToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    functionToken,
                    $"Invalid operand: {functionToken}");
            }

            return new(
                new(tokens[0]),
                new(functionToken),
                fsn,
                this.currentLocation);
        }
        else
        {
            var functionToken = tokens[1];
            if (functionToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    functionToken,
                    $"Invalid operand: {functionToken}");
            }

            return new(
                new(tokens[0]),
                new(functionToken),
                null,
                this.currentLocation);
        }
    }

    private SignatureInstruction? ParseSignatureInstruction(
        Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens[0],
                $"Missing operand: {tokens[0]}");
            return null;
        }
        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operand: {tokens[2]}");
            return null;
        }

        var signatureToken = tokens[1];
        if (signatureToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                signatureToken,
                $"Invalid function signature: {signatureToken}");
        }
        if (!TypeParser.TryParse(signatureToken, out var signature) ||
            signature is not FunctionSignatureNode fsn)
        {
            this.OutputError(
                signatureToken,
                $"Invalid function signature: {signatureToken}");
            return null;
        }

        return new(
            new(tokens[0]),
            fsn,
            this.currentLocation);
    }

    private Instruction? ParseInstruction(
        Token[] tokens)
    {
        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operands: {tokens[2]}");
            return null;
        }

        var opCodeToken = tokens[0];
        if (!opCodes.TryGetValue(opCodeToken.Text, out var opCode))
        {
            this.OutputError(
                opCodeToken,
                $"Invalid opcode: {opCodeToken}");
            return null;
        }

        switch (opCode.OperandType)
        {
            case OperandType.InlineBrTarget:
            case OperandType.ShortInlineBrTarget:
                return this.ParseIdentityInstruction(
                    tokens,
                    "function name",
                    (ot, it, loc) => new BranchInstruction(ot, it, loc));
 
            case OperandType.InlineField:
                return this.ParseIdentityInstruction(
                    tokens,
                    "field name",
                    (ot, it, loc) => new FieldInstruction(ot, it, loc));
   
            case OperandType.InlineTok:
                return this.ParseIdentityInstruction(
                    tokens,
                    "metadata token",
                    (ot, it, loc) => new MetadataTokenInstruction(ot, it, loc));
   
            case OperandType.InlineType:
                return this.ParseTypeInstruction(
                    tokens);

            case OperandType.InlineI:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => int.TryParse(
                        str,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var v) ? v : null);

            case OperandType.ShortInlineI:
                return opCode == OpCodes.Ldc_I4_S ?
                    this.ParseNumericValueInstruction(
                        tokens,
                        str => sbyte.TryParse(
                            str,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var v) ? v : null) :
                    this.ParseNumericValueInstruction(
                        tokens,
                        str => byte.TryParse(
                            str,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var v) ? v : null);
  
            case OperandType.InlineI8:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => long.TryParse(
                        str,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var v) ? v : null);
  
            case OperandType.InlineR:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => double.TryParse(
                        str,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var v) ? v : null);

            case OperandType.ShortInlineR:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => float.TryParse(
                        str,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var v) ? v : null);

            case OperandType.InlineString:
                return this.ParseStringValueInstruction(
                    tokens);
   
            case OperandType.InlineVar:
                return this.ParseVariableInstruction(
                    tokens,
                    false);
            
            case OperandType.ShortInlineVar:
                return this.ParseVariableInstruction(
                    tokens,
                    true);

            case OperandType.InlineMethod:
                return this.ParseCallInstruction(
                    tokens);
            
            case OperandType.InlineSig:
                return this.ParseSignatureInstruction(
                    tokens);
 
            case OperandType.InlineNone:
                if (tokens.Length > 1)
                {
                    this.OutputError(
                        tokens[1],
                        $"Invalid operand: {tokens[1]}");
                    return null;
                }
                
                return new SingleInstruction(
                    new(opCodeToken),
                    this.currentLocation);
            
            //case OperandType.InlineSwitch:
            //  break;
            
            default:
                this.OutputError(
                    opCodeToken,
                    $"Unsupported opcode: {opCodeToken}");
                return null;
        }
    }

    /////////////////////////////////////////////////////////////////////

    private readonly struct FunctionBodyResults
    {
        public readonly LocalVariableNode[] LocalVariables;
        public readonly LogicalInstruction[] Instructions;

        public FunctionBodyResults(
            LocalVariableNode[] localVariables,
            LogicalInstruction[] instructions)
        {
            this.LocalVariables = localVariables;
            this.Instructions = instructions;
        }

        public void Deconstruct(
            out LocalVariableNode[] localVariables,
            out LogicalInstruction[] instructions)
        {
            localVariables = this.LocalVariables;
            instructions = this.Instructions;
        }
    }

    private FunctionBodyResults ParseFunctionBody(
        TokensIterator tokensIterator)
    {
        var localVariables = new List<LocalVariableNode>();
        var instructions = new List<LogicalInstruction>();

        while (tokensIterator.TryGetNext(out var tokens))
        {
            var token0 = tokens[0];
            switch (token0)
            {
                // Label:
                case (TokenTypes.Label, _):
                    if (this.ParseLabel(tokens) is { } label)
                    {
                        instructions.Add(label);
                    }
                    continue;

                // Instruction:
                case (TokenTypes.Identity, _):
                    if (this.ParseInstruction(tokens) is { } instruction)
                    {
                        instructions.Add(instruction);
                    }
                    continue;

                // Local variable directive:
                case (TokenTypes.Directive, "local"):
                    if (this.ParseLocalDirective(tokens) is { } localVariable)
                    {
                        localVariables.Add(localVariable);
                    }
                    continue;
                
                // Location directive:
                case (TokenTypes.Directive, "location"):
                    if (this.ParseLocationDirective(tokens) is { } location)
                    {
                        this.currentLocation = location;
                    }
                    continue;
                
                // Hidden directive:
                case (TokenTypes.Directive, "hidden"):
                    if (this.ParseHiddenDirective(tokens))
                    {
                        this.currentLocation = null;
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

        return new(
            localVariables.ToArray(),
            instructions.ToArray());
    }
}
