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
using System.Diagnostics;
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
            return Utilities.Empty<Token>();
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
            else if (this.variableDebugInformationLists.TryGetValue(this.method!.Name, out var list) &&
                list.FirstOrDefault(vdi => vdi.Name == vop.Text) is { } vdi)
            {
                vi = vdi.Index;
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
            else if (this.method!.Parameters.
                FirstOrDefault(p => p.Name == aop.Text) is { } parameter)
            {
                return Instruction.Create(opCode, parameter);
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
            var functionName = tokens[1].Text;
            var functionParameterTypeNames =
                tokens.Skip(2).Select(token => token.Text).ToArray();

            if (this.TryGetMethod(
                functionName, functionParameterTypeNames, out var method))
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(method));
            }
            else if (tokens.Length >= 3)
            {
                this.OutputError(
                    tokens[1],
                    $"Could not find method: {functionName}");
            }
            else
            {
                var dummyMethod = new MethodDefinition(
                    "_dummy",
                    MethodAttributes.Private | MethodAttributes.Abstract,
                    this.module.TypeSystem.Void);
                var instruction = Instruction.Create(
                    opCode, dummyMethod);

                var capturedLocation = currentLocation;
                this.delayedLookupLocalMemberActions.Add(() =>
                {
                    if (this.TryGetMethod(
                        functionName, functionParameterTypeNames, out var method2))
                    {
                        instruction.Operand = method2;
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
            var fieldName = fop.Text;

            if (this.TryGetField(
                fieldName, out var field))
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(field));
            }
            else
            {
                var dummyField = new FieldDefinition(
                    "_dummy",
                    FieldAttributes.Private | FieldAttributes.Literal,
                    this.module.TypeSystem.Int32);
                var instruction = Instruction.Create(
                    opCode, dummyField);

                var capturedLocation = currentLocation;
                this.delayedLookupLocalMemberActions.Add(() =>
                {
                    if (this.TryGetField(
                        fieldName, out var field2))
                    {
                        instruction.Operand = field2;
                    }
                    else
                    {
                        this.OutputError(
                            capturedLocation,
                            $"Could not find global variable: {fieldName}");
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
            var typeName = top.Text;

            if (!this.TryGetType(typeName, out var type))
            {
                this.OutputError(
                    top,
                    $"Could not find type: {typeName}");
            }
            else
            {
                return Instruction.Create(
                    opCode, this.module.ImportReference(type));
            }
        }
        return null;
    }

    private Instruction? ParseInlineToken(
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
            Instruction? GetMember(
                string memberName, string[] functionParameterTypeNames,
                Func<FieldReference, Instruction> foundField,
                Func<MethodReference, Instruction> foundMethod,
                Func<TypeReference, Instruction> foundType)
            {
                if (this.TryGetMethod(
                    memberName, functionParameterTypeNames, out var method))
                {
                    return foundMethod(this.module.ImportReference(method));
                }
                else if (this.TryGetField(
                    memberName, out var field))
                {
                    return foundField(this.module.ImportReference(field));
                }
                else if (this.TryGetType(
                    memberName, out var type))
                {
                    return foundType(this.module.ImportReference(type));
                }
                else
                {
                    return null;
                }
            }

            var memberName = tokens[1].Text;
            var functionParameterTypeNames =
                tokens.Skip(2).Select(token => token.Text).ToArray();

            if (GetMember(
                memberName, functionParameterTypeNames,
                fr => Instruction.Create(opCode, fr),
                mr => Instruction.Create(opCode, mr),
                tr => Instruction.Create(opCode, tr)) is { } instruction)
            {
                return instruction;
            }
            else if (functionParameterTypeNames.Length >= 1)
            {
                this.OutputError(
                    tokens[1],
                    $"Could not find member: {memberName}");
            }
            else
            {
                var dummyField = new FieldDefinition(
                    "_dummy",
                    FieldAttributes.Private | FieldAttributes.Literal,
                    this.module.TypeSystem.Int32);
                instruction = Instruction.Create(
                    opCode, dummyField);

                var capturedLocation = currentLocation;
                this.delayedLookupLocalMemberActions.Add(() =>
                {
                    Instruction SetInstruction<T>(T member)
                        where T : MemberReference
                    {
                        instruction.Operand = member;
                        return instruction;
                    }

                    if (GetMember(
                        memberName, functionParameterTypeNames,
                        SetInstruction,
                        SetInstruction,
                        SetInstruction) == null)
                    {
                        this.OutputError(
                            capturedLocation,
                            $"Could not find global variable: {memberName}");
                    }
                });

                return instruction;
            }
        }

        return null;
    }

    private Instruction? ParseInlineSig(
        OpCode opCode, Token[] tokens)
    {
        if (tokens.Length <= 1)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing operand.");
        }
        else
        {
            var returnTypeName = tokens[1].Text;
            var parameterTypeNames =
                tokens.Skip(2).Select(token => token.Text).ToArray();

            if (!this.TryGetType(returnTypeName, out var returnType))
            {
                this.OutputError(
                    tokens[1],
                    $"Could not find type: {returnTypeName}");
            }
            else
            {
                var callSite = new CallSite(returnType);
                foreach (var parameterTypeToken in tokens.Skip(2))
                {
                    if (this.TryGetType(parameterTypeToken.Text, out var parameterType))
                    {
                        callSite.Parameters.Add(new(parameterType));
                    }
                    else
                    {
                        this.OutputError(
                            parameterTypeToken,
                            $"Could not find type: {parameterTypeToken.Text}");
                    }
                }

                return Instruction.Create(
                    opCode, callSite);
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
        }
        else if (this.isProducedOriginalSourceCodeLocation)
        {
            this.locationByInstructions.Add(
                instruction,
                currentLocation);
        }
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
                    this.basePath,
                    this.relativePath,
                    tokens[0].Line,
                    tokens[0].StartColumn,
                    tokens[tokens.Length - 1].Line,
                    tokens[tokens.Length - 1].EndColumn,
                    DocumentLanguage.Cil);

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
                    this.ParseInlineMethod(opCode, tokens, currentLocation),
                OperandType.InlineField =>
                    this.ParseInlineField(opCode, tokens, currentLocation),
                OperandType.InlineType =>
                    this.ParseInlineType(opCode, tokens),
                OperandType.InlineTok =>
                    this.ParseInlineToken(opCode, tokens, currentLocation),
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
}
