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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

// System.Reflection.Emit declarations are used only referring OpCode metadata.
using System.Reflection.Emit;

namespace chibicc.toolchain.Parsing;

partial class CilParser
{
    private static readonly Dictionary<string, OpCode> opCodes =
        ReflectionEmitDefinition.GetOpCodes();

    private static readonly HashSet<string> isArgumentIndirectingOpCode =
        new(opCodes.Where(entry =>
            entry.Value.OperandType != OperandType.InlineNone &&
            (entry.Key.StartsWith("ldarg") || entry.Key.StartsWith("starg"))).
            Select(entry => entry.Key),
            StringComparer.OrdinalIgnoreCase);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static IReadOnlyDictionary<short, string> GetOpCodeTranslator() =>
        opCodes.ToDictionary(
            entry => entry.Value.Value,
            entry => entry.Key);

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
            Select(token => CommonUtilities.TryParseUInt32(
                token.Text,
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

            return new(localType, new(localNameToken));
        }
        else
        {
            return new(localType, null);
        }
    }

    /////////////////////////////////////////////////////////////////////

    private LabelNode? ParseLabel(
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
            labelToken.Text,
            labelToken);
    }

    private InstructionNode? ParseIdentityInstruction(
        Token[] tokens,
        string displayName,
        Func<IdentityNode, IdentityNode, Location?, InstructionNode?> generator,
        Location? location)
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
            location);
    }

    private TypeInstructionNode? ParseTypeInstruction(
        Token[] tokens,
        LabelNode[] labels,
        Location? location)
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
            labels,
            location);
    }

    private InstructionNode? ParseMetadataTokenInstruction(
        Token[] tokens,
        LabelNode[] labels,
        Location? location)
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
                    $"Invalid metadata token signature: {signatureToken}");
                return null;
            }

            if (!TypeParser.TryParse(signatureToken, out var signature) ||
                signature is not FunctionSignatureNode fsn)
            {
                this.OutputError(
                    signatureToken,
                    $"Invalid function signature: {signatureToken}");
                return null;
            }

            var identityToken = tokens[2];
            if (identityToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    identityToken,
                    $"Invalid metadata token: {identityToken}");
                return null;
            }

            return new MetadataTokenInstructionNode(
                new(tokens[0]),
                new(identityToken),
                fsn,
                labels,
                location);
        }
        else
        {
            var identityToken = tokens[1];
            if (identityToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    identityToken,
                    $"Invalid metadata token: {identityToken}");
                return null;
            }

            return new MetadataTokenInstructionNode(
                new(tokens[0]),
                new(identityToken),
                null,
                labels,
                location);
        }
    }

    private NumericValueInstructionNode? ParseNumericValueInstruction(
        Token[] tokens,
        Func<string, object?> numericParser,
        LabelNode[] labels,
        Location? location)
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
            labels,
            location);
    }

    private StringValueInstructionNode? ParseStringValueInstruction(
        Token[] tokens,
        LabelNode[] labels,
        Location? location)
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
            labels,
            location);
    }

    private IndirectReferenceInstructionNode? ParseVariableInstruction(
        Token[] tokens,
        bool isShort,
        LabelNode[] labels,
        Location? location)
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

        var isArgumentIndirection = isArgumentIndirectingOpCode.
            Contains(tokens[0].Text);

        if (isShort && CommonUtilities.TryParseUInt8(
            variableIdentityToken.Text,
            out var u8))
        {
            return new IndexReferenceInstructionNode(
                new(tokens[0]),
                isArgumentIndirection,
                new(u8, variableIdentityToken),
                labels,
                location);
        }

        if (!isShort && CommonUtilities.TryParseUInt32(
            variableIdentityToken.Text,
            out var u32))
        {
            return new IndexReferenceInstructionNode(
                new(tokens[0]),
                isArgumentIndirection,
                new(u32, variableIdentityToken),
                labels,
                location);
        }

        return new NameReferenceInstructionNode(
            new(tokens[0]),
            isArgumentIndirection,
            new(variableIdentityToken),
            labels,
            location);
    }

    private CallInstructionNode? ParseCallInstruction(
        Token[] tokens,
        LabelNode[] labels,
        Location? location)
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
                labels,
                location);
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
                labels,
                location);
        }
    }

    private SignatureInstructionNode? ParseSignatureInstruction(
        Token[] tokens,
        LabelNode[] labels,
        Location? location)
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
            labels,
            location);
    }

    private InstructionNode? ParseInstruction(
        Token[] tokens,
        LabelNode[] labels,
        Location? location)
    {
        if (tokens.Length > 3)
        {
            this.OutputError(
                tokens[3],
                $"Too many operands: {tokens[3]}");
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
                    (ot, it, loc) => new BranchInstructionNode(ot, it, labels, loc),
                    location);
 
            case OperandType.InlineField:
                return this.ParseIdentityInstruction(
                    tokens,
                    "field name",
                    (ot, it, loc) => new FieldInstructionNode(ot, it, labels, loc),
                    location);
   
            case OperandType.InlineTok:
                return this.ParseMetadataTokenInstruction(
                    tokens,
                    labels,
                    location);
   
            case OperandType.InlineType:
                return this.ParseTypeInstruction(
                    tokens,
                    labels,
                    location);

            case OperandType.InlineI:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => CommonUtilities.TryParseInt32(
                        str,
                        out var v) ? v : null,
                    labels,
                    location);

            case OperandType.ShortInlineI:
                return opCode == OpCodes.Ldc_I4_S ?
                    this.ParseNumericValueInstruction(
                        tokens,
                        str => CommonUtilities.TryParseInt8(
                            str,
                            out var v) ? v : null,
                        labels,
                        location) :
                    this.ParseNumericValueInstruction(
                        tokens,
                        str => CommonUtilities.TryParseUInt8(
                            str,
                            out var v) ? v : null,
                        labels,
                        location);
  
            case OperandType.InlineI8:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => CommonUtilities.TryParseInt64(
                        str,
                        out var v) ? v : null,
                    labels,
                    location);
  
            case OperandType.InlineR:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => CommonUtilities.TryParseFloat64(
                        str,
                        out var v) ? v : null,
                    labels,
                    location);

            case OperandType.ShortInlineR:
                return this.ParseNumericValueInstruction(
                    tokens,
                    str => CommonUtilities.TryParseFloat32(
                        str,
                        out var v) ? v : null,
                    labels,
                    location);

            case OperandType.InlineString:
                return this.ParseStringValueInstruction(
                    tokens,
                    labels,
                    location);
   
            case OperandType.InlineVar:
                return this.ParseVariableInstruction(
                    tokens,
                    false,
                    labels,
                    location);
            
            case OperandType.ShortInlineVar:
                return this.ParseVariableInstruction(
                    tokens,
                    true,
                    labels,
                    location);

            case OperandType.InlineMethod:
                return this.ParseCallInstruction(
                    tokens,
                    labels,
                    location);
            
            case OperandType.InlineSig:
                return this.ParseSignatureInstruction(
                    tokens,
                    labels,
                    location);
 
            case OperandType.InlineNone:
                if (tokens.Length > 1)
                {
                    this.OutputError(
                        tokens[1],
                        $"Invalid operand: {tokens[1]}");
                    return null;
                }
                
                return new SingleInstructionNode(
                    new(opCodeToken),
                    labels,
                    location);
            
            // TODO:
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
        public readonly InstructionNode[] Instructions;

        public FunctionBodyResults(
            LocalVariableNode[] localVariables,
            InstructionNode[] instructions)
        {
            this.LocalVariables = localVariables;
            this.Instructions = instructions;
        }

        public void Deconstruct(
            out LocalVariableNode[] localVariables,
            out InstructionNode[] instructions)
        {
            localVariables = this.LocalVariables;
            instructions = this.Instructions;
        }
    }

    private FunctionBodyResults ParseFunctionBody(
        TokensIterator tokensIterator)
    {
        var localVariables = new List<LocalVariableNode>();
        var instructions = new List<InstructionNode>();
        var currentInstructionLabels = new List<LabelNode>();
        var overallLabelNames = new HashSet<string>();

        Location? currentLocation = null;

        while (tokensIterator.TryGetNext(out var tokens))
        {
            var token0 = tokens[0];
            switch (token0)
            {
                // Label:
                case (TokenTypes.Label, _):
                    if (this.ParseLabel(tokens) is { } label)
                    {
                        if (!overallLabelNames.Add(label.Name))
                        {
                            this.OutputError(
                                label.Token,
                                $"Duplicated label: {label}");
                        }
                        else
                        {
                            currentInstructionLabels.Add(label);
                        }
                    }
                    continue;

                // Instruction:
                case (TokenTypes.Identity, _):
                    if (this.locationMode == LocationModes.OriginSource)
                    {
                        var lastToken = tokens.Last();
                        currentLocation = new(
                            new(token0.BasePath, token0.RelativePath, Language.Cil, true),
                            token0.Line,
                            token0.StartColumn,
                            lastToken.Line,
                            lastToken.EndColumn);
                    }
                    if (this.ParseInstruction(
                        tokens,
                        currentInstructionLabels.ToArray(),
                        currentLocation) is { } instruction)
                    {
                        instructions.Add(instruction);
                    }
                    currentInstructionLabels.Clear();
                    currentLocation = null;
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
                        this.locationMode = LocationModes.Directive;
                        currentLocation = location;
                    }
                    continue;
                
                // Hidden directive:
                case (TokenTypes.Directive, "hidden"):
                    if (this.ParseHiddenDirective(tokens))
                    {
                        this.locationMode = LocationModes.Hide;
                        currentLocation = null;
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
