/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;

namespace chibias.Parsing;

partial class Parser
{
    private Location GetCurrentLocation(Token startToken, Token endToken) =>
        new(
            this.currentCilFile,
            startToken.Line,
            startToken.StartColumn,
            endToken.Line,
            endToken.EndColumn);

    private Location GetCurrentLocation(Token token) =>
        this.GetCurrentLocation(token, token);

    private Token? FetchOperand0(Token[] tokens)
    {
        if (tokens.Length >= 3)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens[2]),
                $"Too many operands.");
            return null;
        }
        
        if (tokens.Length <= 1)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens.Last()),
                $"Missing operand.");
            return null;
        }

        return tokens[1];
    }

    private Token[]? FetchOperands(Token[] tokens, int count)
    {
        if (tokens.Length >= (2 + count))
        {
            this.OutputError(
                this.GetCurrentLocation(tokens[count + 1]),
                $"Too many operands.");
            return null;
        }
        
        if (tokens.Length <= count)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens.Last()),
                $"Missing operand.");
            return null;
        }
        
        if (count == 0)
        {
            return Utilities.Empty<Token>();
        }

        return tokens.Skip(1).ToArray();
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineNone(
        OpCode opCode, Token[] tokens) =>
        this.FetchOperands(tokens, 0) is { } ?
            Instruction.Create(opCode) : null;

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineI(
        OpCode opCode, Token[] tokens)
    {
        if (this.FetchOperand0(tokens) is { } valueToken)
        {
            if (Utilities.TryParseInt32(valueToken.Text, out var int32Value))
            {
                return Instruction.Create(opCode, int32Value);
            }
            else
            {
                this.OutputError(
                    this.GetCurrentLocation(valueToken),
                    $"Invalid operand: {valueToken.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseShortInlineI(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } valueToken))
        {
            return null;
        }

        if (opCode == OpCodes.Ldc_I4_S) // Only ldc.i4.s
        {
            if (!Utilities.TryParseInt8(valueToken.Text, out var int8Value))
            {
                this.OutputError(
                    this.GetCurrentLocation(valueToken),
                    $"Invalid operand: {valueToken.Text}");
                return null;
            }

            return Instruction.Create(opCode, int8Value);
        }

        if (!Utilities.TryParseUInt8(valueToken.Text, out var uint8Value))
        {
            this.OutputError(
                this.GetCurrentLocation(valueToken),
                $"Invalid operand: {valueToken.Text}");
            return null;
        }

        return Instruction.Create(opCode, uint8Value);
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineI8(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } valueToken))
        {
            return null;
        }

        if (!Utilities.TryParseInt64(valueToken.Text, out var int64Value))
        {
            this.OutputError(
                this.GetCurrentLocation(valueToken),
                $"Invalid operand: {valueToken.Text}");
        }

        return Instruction.Create(opCode, int64Value);
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineR(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } valueToken))
        {
            return null;
        }

        if (!Utilities.TryParseFloat64(valueToken.Text, out var float64Value))
        {
            this.OutputError(
                this.GetCurrentLocation(valueToken),
                $"Invalid operand: {valueToken.Text}");
            return null;
        }

        return Instruction.Create(opCode, float64Value);
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseShortInlineR(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } valueToken))
        {
            return null;
        }

        if (!Utilities.TryParseFloat32(valueToken.Text, out var float32Value))
        {
            this.OutputError(
                this.GetCurrentLocation(valueToken),
                $"Invalid operand: {valueToken.Text}");
            return null;
        }

        return Instruction.Create(opCode, float32Value);
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineBrTarget(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } targetLabelNameToken))
        {
            return null;
        }

        var targetLabelName = targetLabelNameToken.Text;

        var dummyInstruction = Instruction.Create(OpCodes.Nop);
        var instruction = Instruction.Create(opCode, dummyInstruction);

        var capturedLocation =
            this.GetCurrentLocation(targetLabelNameToken);
        this.delayedLookupBranchTargetActions.Enqueue(() =>
        {
            if (this.labelTargets.TryGetValue(targetLabelName, out var targetInstruction))
            {
                instruction.Operand = targetInstruction;
            }
            else
            {
                instruction.Operand = Instruction.Create(OpCodes.Nop);
                this.OutputError(
                    capturedLocation,
                    $"Could not found the label: {targetLabelName}");
            }
        });

        return instruction;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineVar(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } variableIdentityToken))
        {
            return null;
        }

        var variableIdentity = variableIdentityToken.Text;

        if (this.variableDebugInformationLists.TryGetValue(
            this.method!.Name, out var list) &&
            list.FirstOrDefault(vdi => vdi.Name == variableIdentity) is { } vdi)
        {
            return Instruction.Create(opCode, this.body!.Variables[vdi.Index]);
        }
        else if (Utilities.TryParseInt32(variableIdentity, out var variableIndex) &&
            variableIndex < this.body!.Variables.Count)
        {
            return Instruction.Create(opCode, this.body!.Variables[variableIndex]);
        }

        this.OutputError(
            this.GetCurrentLocation(variableIdentityToken),
            $"Invalid operand: {variableIdentity}");
        return null;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineArg(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } argumentIdentityToken))
        {
            return null;
        }

        var argumentIdentity = argumentIdentityToken.Text;

        if (this.method!.Parameters.
            FirstOrDefault(p => p.Name == argumentIdentity) is { } parameter)
        {
            return Instruction.Create(opCode, parameter);
        }
        else if (Utilities.TryParseInt32(argumentIdentity, out var parameterIndex) &&
            parameterIndex < this.method!.Parameters.Count)
        {
            return Instruction.Create(opCode, this.method!.Parameters[parameterIndex]);
        }

        this.OutputError(
            this.GetCurrentLocation(argumentIdentityToken),
            $"Invalid operand: {argumentIdentity}");
        return null;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineMethod(
        OpCode opCode, Token[] tokens)
    {
        if (tokens.Length <= 1)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens.Last()),
                $"Missing operand.");
            return null;
        }
        if (tokens.Length >= 4)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens[3]),
                $"Too many operands.");
            return null;
        }

        var functionNameToken = tokens.Last();
        var functionName = functionNameToken.Text;

        var instruction = Instruction.Create(
            opCode, this.CreateDummyMethod());

        if (tokens.Length == 3)
        {
            var functionSignatureToken = tokens[1];
            var functionSignature = functionSignatureToken.Text;

            if (!TypeParser.TryParse(functionSignature, out var rootTypeNode))
            {
                this.OutputError(
                    this.GetCurrentLocation(functionSignatureToken),
                    $"Invalid function signature {functionSignature}");
                return null;
            }

            if (!(rootTypeNode is FunctionSignatureNode fsn))
            {
                this.OutputError(
                    this.GetCurrentLocation(functionSignatureToken),
                    $"Invalid function signature: {functionSignature}");
                return null;
            }

            this.DelayLookingUpMethod(
                functionNameToken,
                fsn,
                functionSignatureToken,
                LookupTargets.All,
                method => instruction.Operand = method);
        }
        else
        {
            this.DelayLookingUpMethod(
                functionNameToken,
                null,
                functionNameToken,
                LookupTargets.All,
                method => instruction.Operand = method);
        }

        return instruction;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineField(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } fieldNameToken))
        {
            return null;
        }

        var instruction = Instruction.Create(
            opCode, this.CreateDummyField());

        this.DelayLookingUpField(
            fieldNameToken,
            fieldNameToken,
            LookupTargets.All,
            field => instruction.Operand = field);

        return instruction;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineType(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } typeNameToken))
        {
            return null;
        }

        var instruction = Instruction.Create(
            opCode, this.CreateDummyType());

        this.DelayLookingUpType(
            typeNameToken,
            typeNameToken,
            LookupTargets.All,
            type => instruction.Operand = type);

        return instruction;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineToken(
        OpCode opCode, Token[] tokens)
    {
        if (tokens.Length <= 1)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens.Last()),
                $"Missing operand.");
            return null;
        }
        if (tokens.Length >= 4)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens[3]),
                $"Too many operands.");
            return null;
        }

        var memberNameToken = tokens.Last();
        var memberName = memberNameToken.Text;

        var instruction = CecilUtilities.CreateInstruction(
            opCode, this.CreateDummyField());

        if (tokens.Length == 3)
        {
            var functionSignatureToken = tokens[1];
            var functionSignature = functionSignatureToken.Text;

            if (!TypeParser.TryParse(functionSignature, out var rootTypeNode))
            {
                this.OutputError(
                    this.GetCurrentLocation(functionSignatureToken),
                    $"Invalid function signature {functionSignature}");
                return null;
            }

            if (!(rootTypeNode is FunctionSignatureNode fsn))
            {
                this.OutputError(
                    this.GetCurrentLocation(functionSignatureToken),
                    $"Invalid function signature: {functionSignature}");
                return null;
            }

            this.DelayLookingUpMember(
                memberNameToken,
                fsn,
                functionSignatureToken,
                LookupTargets.All,
                method => instruction.Operand = method);
        }
        else
        {
            this.DelayLookingUpMember(
                memberNameToken,
                null,
                memberNameToken,
                LookupTargets.All,
                method => instruction.Operand = method);
        }

        return instruction;
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineSig(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } functionSignatureToken))
        {
            return null;
        }

        var functionSignature = functionSignatureToken.Text;

        if (!TypeParser.TryParse(functionSignature, out var rootTypeNode))
        {
            this.OutputError(
                this.GetCurrentLocation(functionSignatureToken),
                $"Invalid function signature {functionSignature}");
            return null;
        }
        
        if (!(rootTypeNode is FunctionSignatureNode fsn))
        {
            this.OutputError(
                this.GetCurrentLocation(functionSignatureToken),
                $"Invalid function signature: {functionSignature}");
            return null;
        }
        
        var callSite = new CallSite(this.CreateDummyType())
        {
            CallingConvention = fsn.CallingConvention,
            HasThis = false,
            ExplicitThis = false,
        };

        this.DelayLookingUpType(
            fsn.ReturnType,
            functionSignatureToken,
            LookupTargets.All,
            type => callSite.ReturnType = type);

        foreach (var (parameterTypeNode, parameterName) in fsn.Parameters)
        {
            var parameter = new ParameterDefinition(
                this.CreateDummyType());
            parameter.Name = parameterName;   // Not sure if that makes sense.
            callSite.Parameters.Add(parameter);

            this.DelayLookingUpType(
                parameterTypeNode,
                functionSignatureToken,
                LookupTargets.All,
                type => parameter.ParameterType = type);
        }

        return Instruction.Create(opCode, callSite);
    }

    /////////////////////////////////////////////////////////////////////

    private Instruction? ParseInlineString(
        OpCode opCode, Token[] tokens)
    {
        if (!(this.FetchOperand0(tokens) is { } stringLiteralToken))
        {
            return null;
        }

        var stringLiteral = stringLiteralToken.Text;
        return Instruction.Create(opCode, stringLiteral);
    }

    /////////////////////////////////////////////////////////////////////

    private void AppendInstruction(
        Instruction instruction, Location currentLocation)
    {
        this.instructions!.Add(instruction);

        foreach (var labelName in this.willApplyLabelingNames)
        {
            this.labelTargets.Add(labelName, instruction);
        }
        this.willApplyLabelingNames.Clear();

        if (this.queuedLocation is { } queuedLocation)
        {
            this.queuedLocation = null;
            this.lastLocation = queuedLocation;
            this.locationByInstructions.Add(
                instruction,
                queuedLocation);
            return;
        }

        if (this.isProducedOriginalSourceCodeLocation)
        {
            this.locationByInstructions.Add(
                instruction,
                currentLocation);
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseInstruction(OpCode opCode, Token[] tokens)
    {
        // Reached when undeclared function directive.
        if (this.instructions == null)
        {
            this.OutputError(
                this.GetCurrentLocation(tokens[0]),
                $"Function directive is not defined.");
            return;
        }

        // In function directive:
        var currentLocation =
            this.queuedLocation ??
            this.lastLocation ??
            new Location(
                this.currentCilFile,
                tokens[0].Line,
                tokens[0].StartColumn,
                tokens[tokens.Length - 1].Line,
                tokens[tokens.Length - 1].EndColumn);

        // Will fetch valid operand types:
        var instruction = opCode.OperandType switch
        {
            OperandType.InlineNone =>
                this.ParseInlineNone(opCode, tokens),
            OperandType.InlineI =>
                this.ParseInlineI(opCode, tokens),
            OperandType.ShortInlineI =>
                this.ParseShortInlineI(opCode, tokens),
            OperandType.InlineI8 =>
                this.ParseInlineI8(opCode, tokens),
            OperandType.InlineR =>
                this.ParseInlineR(opCode, tokens),
            OperandType.ShortInlineR =>
                this.ParseShortInlineR(opCode, tokens),
            OperandType.InlineBrTarget =>
                this.ParseInlineBrTarget(opCode, tokens),
            OperandType.ShortInlineBrTarget =>
                this.ParseInlineBrTarget(opCode, tokens),
            OperandType.InlineVar =>
                this.ParseInlineVar(opCode, tokens),
            OperandType.ShortInlineVar =>
                this.ParseInlineVar(opCode, tokens),
            OperandType.InlineArg =>
                this.ParseInlineArg(opCode, tokens),
            OperandType.ShortInlineArg =>
                this.ParseInlineArg(opCode, tokens),
            OperandType.InlineMethod =>
                this.ParseInlineMethod(opCode, tokens),
            OperandType.InlineField =>
                this.ParseInlineField(opCode, tokens),
            OperandType.InlineType =>
                this.ParseInlineType(opCode, tokens),
            OperandType.InlineTok =>
                this.ParseInlineToken(opCode, tokens),
            OperandType.InlineSig =>
                this.ParseInlineSig(opCode, tokens),
            OperandType.InlineString =>
                this.ParseInlineString(opCode, tokens),
            _ =>
                null,
        };

        if (instruction != null)
        {
            this.AppendInstruction(instruction, currentLocation);
        }
    }
}
