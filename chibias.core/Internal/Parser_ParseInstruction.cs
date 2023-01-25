/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace chibias.Internal;

partial class Parser
{
    private Token? FetchOperand0(Token[] tokens)
    {
        if (tokens.Length >= 3)
        {
            this.OutputError(
                tokens[2],
                $"Too many operands.");
            return null;
        }
        else if (tokens.Length <= 1)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing operand.");
            return null;
        }
        else
        {
            return tokens[1];
        }
    }

    private Token[]? FetchOperands(Token[] tokens, int count)
    {
        if (tokens.Length >= (2 + count))
        {
            this.OutputError(
                tokens[count + 1],
                $"Too many operands.");
            return null;
        }
        else if (tokens.Length <= count)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing operand.");
            return null;
        }
        else if (count == 0)
        {
            return Array.Empty<Token>();
        }
        else
        {
            return tokens.Skip(1).ToArray();
        }
    }

    private Instruction? ParseInlineNone(
        OpCode opCode, Token[] tokens) =>
        FetchOperands(tokens, 0) is { } ?
            Instruction.Create(opCode) :
            null;

    private Instruction? ParseInlineI(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } iop)
        {
            if (Utilities.TryParseInt32(iop.Text, out var i))
            {
                return Instruction.Create(opCode, i);
            }
            else
            {
                this.OutputError(
                    iop,
                    $"Invalid operand: {iop.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseShortInlineI(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } siop)
        {
            if (opCode == OpCodes.Ldc_I4_S) // Only ldc.i4.s
            {
                if (Utilities.TryParseInt8(siop.Text, out var sb))
                {
                    return Instruction.Create(opCode, sb);
                }
                else
                {
                    this.OutputError(
                        siop,
                        $"Invalid operand: {siop.Text}");
                }
            }
            else
            {
                if (Utilities.TryParseUInt8(siop.Text, out var b))
                {
                    return Instruction.Create(opCode, b);
                }
                else
                {
                    this.OutputError(
                        siop,
                        $"Invalid operand: {siop.Text}");
                }
            }
        }
        return null;
    }

    private Instruction? ParseInlineI8(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } lop)
        {
            if (Utilities.TryParseInt64(lop.Text, out var l))
            {
                return Instruction.Create(opCode, l);
            }
            else
            {
                this.OutputError(
                    lop,
                    $"Invalid operand: {lop.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseInlineR(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } rop)
        {
            if (Utilities.TryParseFloat64(rop.Text, out var d))
            {
                return Instruction.Create(opCode, d);
            }
            else
            {
                this.OutputError(
                    rop,
                    $"Invalid operand: {rop.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseShortInlineR(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } srop)
        {
            if (Utilities.TryParseFloat32(srop.Text, out var f))
            {
                return Instruction.Create(opCode, f);
            }
            else
            {
                this.OutputError(
                    srop,
                    $"Invalid operand: {srop.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseInlineBrTarget(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } brop)
        {
            var dummyInstruction = Instruction.Create(OpCodes.Nop);
            var instruction = Instruction.Create(opCode, dummyInstruction);

            var capturedLocation = this.queuedLocation;
            this.delayedLookupBranchTargetActions.Add(() =>
            {
                if (this.labelTargets.TryGetValue(brop.Text, out var target))
                {
                    instruction.Operand = target;
                }
                else
                {
                    instruction.Operand = Instruction.Create(OpCodes.Nop);
                    this.OutputError(
                        brop,
                        $"Could not found the label: {brop.Text}");
                }
            });
            return instruction;
        }
        return null;
    }

    private Instruction? ParseInlineVar(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } vop)
        {
            if (Utilities.TryParseInt32(vop.Text, out var vi) &&
                vi < this.body!.Variables.Count)
            {
                return Instruction.Create(opCode, this.body!.Variables[vi]);
            }
            else
            {
                this.OutputError(
                    vop,
                    $"Invalid operand: {vop.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseInlineArg(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } aop)
        {
            if (Utilities.TryParseInt32(aop.Text, out var pi) &&
                pi < this.method!.Parameters.Count)
            {
                return Instruction.Create(opCode, this.method!.Parameters[pi]);
            }
            else
            {
                this.OutputError(
                    aop,
                    $"Invalid operand: {aop.Text}");
            }
        }
        return null;
    }

    private Instruction? ParseInlineMethod(
        OpCode opCode, Token[] tokens, Location currentLocation)
    {
        if (tokens.Length <= 1)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing operand.");
        }
        else
        {
            if (this.cabiSpecificSymbols.TryGetValue(
                tokens[1].Text, out var member) &&
                member is MethodDefinition method &&
                tokens.Length == 2)
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(method));
            }
            else if (this.TryGetMethod(
                tokens[1].Text,
                tokens.Skip(2).Select(token => token.Text),
                out var method2))
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(method2));
            }
            else if (tokens.Length >= 3)
            {
                this.OutputError(
                    tokens[1],
                    $"Could not find method: {tokens[1].Text}");
            }
            else
            {
                var dummyMethod = new MethodDefinition(
                    "_dummy",
                    MethodAttributes.Private | MethodAttributes.Abstract,
                    this.module.TypeSystem.Void);
                var instruction = Instruction.Create(
                    opCode, dummyMethod);

                var functionName = tokens[1].Text;
                var capturedLocation = currentLocation;
                this.delayedLookupLocalMemberActions.Add(() =>
                {
                    if (this.cabiSpecificModuleType.Methods.
                        FirstOrDefault(method => method.Name == functionName) is { } method)
                    {
                        instruction.Operand = method;
                    }
                    else
                    {
                        this.OutputError(
                            capturedLocation,
                            $"Could not find function: {functionName}");
                    }
                });

                return instruction;
            }
        }

        return null;
    }

    private Instruction? ParseInlineField(
        OpCode opCode, Token[] tokens, Location currentLocation)
    {
        if (FetchOperand0(tokens) is { } fop)
        {
            if (this.cabiSpecificSymbols.TryGetValue(fop.Text, out var member) &&
                member is FieldReference field)
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(field));
            }
            else if (this.TryGetField(
                tokens[1].Text,
                out var field2))
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(field2));
            }
            else
            {
                var dummyField = new FieldDefinition(
                    "_dummy",
                    FieldAttributes.Private | FieldAttributes.Literal,
                    this.module.TypeSystem.Int32);
                var instruction = Instruction.Create(
                    opCode, dummyField);

                var globalName = fop.Text;
                var capturedLocation = currentLocation;
                this.delayedLookupLocalMemberActions.Add(() =>
                {
                    if (this.cabiSpecificModuleType.Fields.
                        FirstOrDefault(field => field.Name == globalName) is { } field)
                    {
                        instruction.Operand = field;
                    }
                    else
                    {
                        this.OutputError(
                            capturedLocation,
                            $"Could not find global variable: {globalName}");
                    }
                });

                return instruction;
            }
        }

        return null;
    }

    private Instruction? ParseInlineType(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } top)
        {
            if (!this.TryGetType(top.Text, out var type))
            {
                this.OutputError(
                    top,
                    $"Could not find type: {top.Text}");
            }
            else
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(type));
            }
        }
        return null;
    }

    private Instruction? ParseInlineString(
        OpCode opCode, Token[] tokens)
    {
        if (FetchOperand0(tokens) is { } sop)
        {
            var stringLiteral = sop.Text;
            return Instruction.Create(
                opCode, stringLiteral);
        }

        return null;
    }

    private void ParseInstruction(OpCode opCode, Token[] tokens)
    {
        // Reached when undeclared function directive.
        if (this.instructions == null)
        {
            this.OutputError(
                tokens[0],
                $"Function directive is not defined.");
        }
        // In function directive:
        else
        {
            var currentLocation =
                this.queuedLocation ??
                this.lastLocation ??
                new Location(
                    this.relativePath,
                    tokens[0].Line,
                    tokens[0].StartColumn,
                    tokens[tokens.Length - 1].Line,
                    tokens[tokens.Length - 1].EndColumn,
                    DocumentLanguage.Cil);

            // Will fetch valid operand types:
            Instruction? instruction = null;
            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    instruction = ParseInlineNone(opCode, tokens);
                    break;
                case OperandType.InlineI:
                    instruction = ParseInlineI(opCode, tokens);
                    break;
                case OperandType.ShortInlineI:
                    instruction = ParseShortInlineI(opCode, tokens);
                    break;
                case OperandType.InlineI8:
                    instruction = ParseInlineI8(opCode, tokens);
                    break;
                case OperandType.InlineR:
                    instruction = ParseInlineR(opCode, tokens);
                    break;
                case OperandType.ShortInlineR:
                    instruction = ParseShortInlineR(opCode, tokens);
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.ShortInlineBrTarget:
                    instruction = ParseInlineBrTarget(opCode, tokens);
                    break;
                case OperandType.InlineVar:
                case OperandType.ShortInlineVar:
                    instruction = ParseInlineVar(opCode, tokens);
                    break;
                case OperandType.InlineArg:
                case OperandType.ShortInlineArg:
                    instruction = ParseInlineArg(opCode, tokens);
                    break;
                case OperandType.InlineMethod:
                    instruction = ParseInlineMethod(opCode, tokens, currentLocation);
                    break;
                case OperandType.InlineField:
                case OperandType.InlineTok:
                    instruction = ParseInlineField(opCode, tokens, currentLocation);
                    break;
                case OperandType.InlineType:
                    instruction = ParseInlineType(opCode, tokens);
                    break;
                case OperandType.InlineString:
                    instruction = ParseInlineString(opCode, tokens);
                    break;
            }

            if (instruction != null)
            {
                this.instructions.Add(instruction);

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
                }
                else if (this.isProducedOriginalSourceCodeLocation)
                {
                    this.locationByInstructions.Add(
                        instruction,
                        currentLocation);
                }
            }
        }
    }
}
