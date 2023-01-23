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
using System.Xml.Linq;

namespace chibias.Internal;

partial class Parser
{
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

            Token? FetchOperand0()
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

            Token[]? FetchOperands(int count)
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

            // Will fetch valid operand types:
            Instruction? instruction = null;
            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    if (FetchOperands(0) is { })
                    {
                        instruction = Instruction.Create(opCode);
                    }
                    break;
                case OperandType.InlineI:
                    if (FetchOperand0() is { } iop)
                    {
                        if (Utilities.TryParseInt32(iop.Text, out var i))
                        {
                            instruction = Instruction.Create(opCode, i);
                        }
                        else
                        {
                            this.OutputError(
                                iop,
                                $"Invalid operand: {iop.Text}");
                        }
                    }
                    break;
                case OperandType.ShortInlineI:
                    if (FetchOperand0() is { } siop)
                    {
                        if (opCode == OpCodes.Ldc_I4_S) // Only ldc.i4.s
                        {
                            if (Utilities.TryParseInt8(siop.Text, out var sb))
                            {
                                instruction = Instruction.Create(opCode, sb);
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
                                instruction = Instruction.Create(opCode, b);
                            }
                            else
                            {
                                this.OutputError(
                                    siop,
                                    $"Invalid operand: {siop.Text}");
                            }
                        }
                    }
                    break;
                case OperandType.InlineI8:
                    if (FetchOperand0() is { } lop)
                    {
                        if (Utilities.TryParseInt64(lop.Text, out var l))
                        {
                            instruction = Instruction.Create(opCode, l);
                        }
                        else
                        {
                            this.OutputError(
                                lop,
                                $"Invalid operand: {lop.Text}");
                        }
                    }
                    break;
                case OperandType.InlineR:
                    if (FetchOperand0() is { } rop)
                    {
                        if (Utilities.TryParseFloat64(rop.Text, out var d))
                        {
                            instruction = Instruction.Create(opCode, d);
                        }
                        else
                        {
                            this.OutputError(
                                rop,
                                $"Invalid operand: {rop.Text}");
                        }
                    }
                    break;
                case OperandType.ShortInlineR:
                    if (FetchOperand0() is { } srop)
                    {
                        if (Utilities.TryParseFloat32(srop.Text, out var f))
                        {
                            instruction = Instruction.Create(opCode, f);
                        }
                        else
                        {
                            this.OutputError(
                                srop,
                                $"Invalid operand: {srop.Text}");
                        }
                    }
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.ShortInlineBrTarget:
                    if (FetchOperand0() is { } brop)
                    {
                        var dummyInstruction = Instruction.Create(OpCodes.Nop);
                        instruction = Instruction.Create(opCode, dummyInstruction);

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
                    }
                    break;
                case OperandType.InlineVar:
                case OperandType.ShortInlineVar:
                    if (FetchOperand0() is { } vop)
                    {
                        if (Utilities.TryParseInt32(vop.Text, out var vi) &&
                            vi < this.body!.Variables.Count)
                        {
                            instruction = Instruction.Create(opCode, this.body!.Variables[vi]);
                        }
                        else
                        {
                            this.OutputError(
                                vop,
                                $"Invalid operand: {vop.Text}");
                        }
                    }
                    break;
                case OperandType.InlineArg:
                case OperandType.ShortInlineArg:
                    if (FetchOperand0() is { } aop)
                    {
                        if (Utilities.TryParseInt32(aop.Text, out var pi) &&
                            pi < this.method!.Parameters.Count)
                        {
                            instruction = Instruction.Create(opCode, this.method!.Parameters[pi]);
                        }
                        else
                        {
                            this.OutputError(
                                aop,
                                $"Invalid operand: {aop.Text}");
                        }
                    }
                    break;
                case OperandType.InlineMethod:
                    if (tokens.Length <= 1)
                    {
                        this.OutputError(
                            tokens.Last(),
                            $"Missing operand.");
                    }
                    else if (this.cabiSpecificSymbols.TryGetValue(tokens[1].Text, out var member) &&
                        member is MethodDefinition method &&
                        tokens.Length == 2)
                    {
                        instruction = Instruction.Create(
                            opCode, this.module.ImportReference(method));
                    }
                    else if (this.TryGetMethod(
                        tokens[1].Text,
                        tokens.Skip(2).Select(token => token.Text),
                        out var method2))
                    {
                        instruction = Instruction.Create(
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
                        instruction = Instruction.Create(
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
                    }
                    break;
                case OperandType.InlineField:
                    if (FetchOperand0() is { } fop)
                    {
                        if (this.cabiSpecificSymbols.TryGetValue(fop.Text, out var member))
                        {
                            if (!(member is FieldReference field))
                            {
                                this.OutputError(
                                    fop,
                                    $"Could not find global variable: {fop.Text}");
                            }
                            else
                            {
                                instruction = Instruction.Create(
                                    opCode, this.module.ImportReference(field));
                            }
                        }
                        else
                        {
                            var dummyField = new FieldDefinition(
                                "_dummy",
                                FieldAttributes.Private | FieldAttributes.Literal,
                                this.module.TypeSystem.Int32);
                            instruction = Instruction.Create(
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
                        }
                    }
                    break;
                case OperandType.InlineType:
                    if (FetchOperand0() is { } top)
                    {
                        if (!this.TryGetType(top.Text, out var type))
                        {
                            this.OutputError(
                                top,
                                $"Could not find type: {top.Text}");
                        }
                        else
                        {
                            instruction = Instruction.Create(
                                opCode, this.module.ImportReference(type));
                        }
                    }
                    break;
                case OperandType.InlineString:
                    if (FetchOperand0() is { } sop)
                    {
                        var stringLiteral = sop.Text;
                        instruction = Instruction.Create(
                            opCode, stringLiteral);
                    }
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
