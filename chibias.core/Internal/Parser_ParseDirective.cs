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
using System.IO;
using System.Linq;

namespace chibias.Internal;

partial class Parser
{
    private MethodDefinition SetupFunctionBodyDirective(
        string functionName,
        TypeReference returnType,
        ParameterDefinition[] parameters,
        bool isPublic)
    {
        this.FinishCurrentState();

        this.method = new MethodDefinition(
            functionName,
            isPublic ?
                (MethodAttributes.Public | MethodAttributes.Static) :
                (MethodAttributes.Private | MethodAttributes.Static),
            this.Import(returnType));
        this.method.HasThis = false;

        foreach (var parameter in parameters)
        {
            this.method.Parameters.Add(parameter);
        }

        this.body = this.method.Body;
        this.body.InitLocals = false;   // Derived C behavior.

        this.instructions = this.body.Instructions;

        return this.method;
    }

    private void ParseFunctionDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length < 3)
        {
            this.OutputError(directive, $"Missing directive operand.");
        }
        else
        {
            var returnTypeName = tokens[1].Text;
            var functionName = tokens[2].Text;

            MethodDefinition method = null!;
            if (!this.TryGetType(returnTypeName, out var returnType))
            {
                returnType = this.CreateDummyType();

                this.DelayLookingUpType(
                    tokens[1],
                    type => method.ReturnType = type);   // (captured)
            }

            var parameters = tokens.Skip(3).
                Collect(parameterToken =>
                {
                    var splitted = parameterToken.Text.Split(':');
                    if (splitted.Length >= 3)
                    {
                        this.OutputError(
                            parameterToken,
                            $"Invalid parameter: {parameterToken.Text}");
                        return null;
                    }
                    else
                    {
                        var parameterTypeName = splitted.Last();

                        ParameterDefinition parameter = null!;
                        if (!this.TryGetType(parameterTypeName, out var parameterType))
                        {
                            parameterType = CreateDummyType();

                            this.DelayLookingUpType(
                                parameterTypeName,
                                parameterToken,
                                type => parameter.ParameterType = type);
                        }

                        parameter = new(parameterType);

                        if (splitted.Length == 2)
                        {
                            parameter.Name = splitted[0];
                        }

                        return parameter;
                    }
                }).
                ToArray();

            method = this.SetupFunctionBodyDirective(
                functionName,
                returnType,
                parameters,
                true);
            this.cabiTextType.Methods.Add(method);
        }
    }

    private void ParseInitializerDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length > 1)
        {
            this.OutputError(directive, $"Too many operands.");
        }
        else
        {
            var functionName = $"initializer_{this.initializers.Count}";

            var initializer = this.SetupFunctionBodyDirective(
                functionName,
                this.module.TypeSystem.Void,
                Utilities.Empty<ParameterDefinition>(),
                false);
            this.cabiDataType.Methods.Add(initializer);

            this.initializers.Add(initializer);
        }
    }

    private void ParseGlobalDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length < 3)
        {
            this.OutputError(directive, $"Missing global variable operand.");
        }
        else
        {
            this.FinishCurrentState();

            var globalTypeName = tokens[1].Text;
            var globalName = tokens[2].Text;

            FieldDefinition field = null!;
            if (!this.TryGetType(globalTypeName, out var globalType))
            {
                globalType = this.CreateDummyType();

                this.DelayLookingUpType(
                    tokens[1],
                    type => field.FieldType = type);   // (captured)
            }

            field = new FieldDefinition(
                globalName,
                FieldAttributes.Public | FieldAttributes.Static,
                globalType);
            this.cabiDataType.Fields.Add(field);
        }
    }

    private void ParseLocalDirective(
        Token directive, Token[] tokens)
    {
        if (this.instructions == null)
        {
            this.OutputError(
                directive,
                $"Function directive is not defined.");
        }
        else if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing local variable operand.");
        }
        else if (tokens.Length > 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else
        {
            var localTypeName = tokens[1].Text;

            VariableDefinition variable = null!;
            if (!this.TryGetType(localTypeName, out var localType))
            {
                localType = this.CreateDummyType();

                this.DelayLookingUpType(
                    tokens[1],
                    type => variable.VariableType = type);   // (captured)
            }

            variable = new VariableDefinition(localType);
            this.body!.Variables.Add(variable);

            if (tokens.Length == 3)
            {
                var localName = tokens[2].Text;
                var variableDebugInformation = new VariableDebugInformation(
                    variable, localName);

                if (!this.variableDebugInformationLists.TryGetValue(
                    this.method!.Name,
                    out var list))
                {
                    list = new();
                    this.variableDebugInformationLists.Add(
                        this.method!.Name,
                        list);
                }

                list.Add(variableDebugInformation);
            }
        }
    }

    private void ParseStructureDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing structure operand.");
        }
        else if (tokens.Length > 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Too many operands.");
        }
        else
        {
            var typeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
            short? packSize = null;
            if (tokens.Length == 3)
            {
                var aligningToken = tokens[2];
                var aligning = aligningToken.Text;
                if (aligning == "explicit")
                {
                    typeAttributes |= TypeAttributes.ExplicitLayout;
                }
                else if (short.TryParse(aligning, out var ps))
                {
                    typeAttributes |= TypeAttributes.SequentialLayout;
                    if (ps >= 1)
                    {
                        packSize = ps;
                    }
                    else
                    {
                        this.OutputError(
                            aligningToken,
                            $"Invalid pack size: {aligning}");
                    }
                }
                else
                {
                    typeAttributes |= TypeAttributes.SequentialLayout;
                }
            }
            else
            {
                typeAttributes |= TypeAttributes.SequentialLayout;
            }

            var structureTypeToken = tokens[1];
            var structureTypeName = structureTypeToken.Text;

            if (this.TryGetType(structureTypeName, out var st))
            {
                // TODO: checks equality
            }
            else
            {
                this.FinishCurrentState();

                var structureType = new TypeDefinition(
                    "C.type",
                    structureTypeName,
                    typeAttributes,
                    this.valueType.Value);
                if (packSize is { } ps)
                {
                    structureType.PackingSize = ps;
                }

                this.module.Types.Add(structureType);
                this.structure = structureType;
            }
        }
    }

    private void ParseConstantDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length <= 2)
        {
            this.OutputError(directive, $"Missing data operand.");
        }
        else
        {
            this.FinishCurrentState();

            var data = tokens.Skip(2).
                Select(token =>
                {
                    if (Utilities.TryParseUInt8(token.Text, out var value))
                    {
                        return value;
                    }
                    else
                    {
                        this.OutputError(token, $"Invalid data operand.");
                        return (byte)0;
                    }
                }).
                ToArray();

            var constantType = this.GetValueArrayType(
                this.module.TypeSystem.SByte,
                data.Length);
            var dataName = tokens[1].Text;

            var field = new FieldDefinition(
                dataName,
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly,
                constantType);
            field.InitialValue = data;

            this.cabiDataType.Fields.Add(field);
        }
    }

    private void ParseFileDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing file operands.");
        }
        else if (tokens.Length > 4)
        {
            this.OutputError(
                tokens[4],
                $"Too many operands.");
        }
        else if (Utilities.TryParseEnum<DocumentLanguage>(tokens[3].Text, out var language))
        {
            if (this.produceDebuggingInformation)
            {
                // NOT Utilities.GetDirectoryPath()
                var file = new FileDescriptor(
                    Path.GetDirectoryName(tokens[2].Text),
                    Path.GetFileName(tokens[2].Text),
                    language);
                this.currentFile = file;
                this.files[tokens[1].Text] = file;
                this.queuedLocation = null;
                this.lastLocation = null;
                this.isProducedOriginalSourceCodeLocation = false;
            }
        }
        else
        {
            this.OutputError(
                tokens[3], $"Invalid language operand: {tokens[3].Text}");
        }
    }

    private void ParseLocationDirective(
        Token directive, Token[] tokens)
    {
        if (this.instructions == null)
        {
            this.OutputError(
                directive,
                $"Function directive is not defined.");
        }
        else if (tokens.Length < 6)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing location operand.");
        }
        else if (tokens.Length > 6)
        {
            this.OutputError(
                tokens[6],
                $"Too many operands.");
        }
        else if (!this.files.TryGetValue(tokens[1].Text, out var file))
        {
            this.OutputError(
                tokens[1],
                $"Unknown file ID.");
        }
        else
        {
            var vs = tokens.
                Skip(2).
                Collect(token =>
                    (int.TryParse(token.Text, out var vi) && vi >= 0) ?
                        vi : default(int?)).
                ToArray();
            if ((vs.Length != (tokens.Length - 2)) ||
                (vs[0] > vs[2]) ||
                (vs[1] >= vs[3]))
            {
                this.OutputError(
                    directive,
                    $"Invalid operand: {tokens[1].Text}");
            }
            else if (this.produceDebuggingInformation)
            {
                var location = new Location(
                    file, vs[0], vs[1], vs[2], vs[3]);
                this.queuedLocation = location;
                this.isProducedOriginalSourceCodeLocation = false;
            }
        }
    }

    private void ParseDirective(Token directive, Token[] tokens)
    {
        switch (directive.Text)
        {
            // Function directive:
            case "function":
                this.ParseFunctionDirective(directive, tokens);
                break;
            // Initializer directive:
            case "initializer":
                this.ParseInitializerDirective(directive, tokens);
                break;
            // Global variable directive:
            case "global":
                this.ParseGlobalDirective(directive, tokens);
                break;
            // Local variable directive:
            case "local":
                this.ParseLocalDirective(directive, tokens);
                break;
            // Structure directive:
            case "structure":
                this.ParseStructureDirective(directive, tokens);
                break;
            // Constant directive:
            case "constant":
                this.ParseConstantDirective(directive, tokens);
                break;
            // File directive:
            case "file":
                this.ParseFileDirective(directive, tokens);
                break;
            // Location directive:
            case "location":
                this.ParseLocationDirective(directive, tokens);
                break;
            // Other, invalid assembler directive.
            default:
                this.OutputError(
                    directive,
                    $"Invalid directive: .{directive.Text}");
                break;
        }
    }
}
