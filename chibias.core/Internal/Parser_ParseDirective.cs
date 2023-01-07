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
using System.Linq;

namespace chibias.Internal;

partial class Parser
{
    private void ParseDirective(Token directive, Token[] tokens)
    {
        switch (directive.Text)
        {
            // Function directive:
            case "function":
                if (tokens.Length <= 2)
                {
                    this.OutputError(directive, $"Missing directive operand.");
                }
                else if (!this.TryGetType(tokens[1].Text, out var returnType))
                {
                    this.OutputError(tokens[1], $"Invalid return type name: {tokens[1].Text}");
                }
                else
                {
                    this.FinishCurrentFunction();

                    var functionName = tokens[2].Text;
                    this.method = new MethodDefinition(
                        functionName,
                        MethodAttributes.Public | MethodAttributes.Static,
                        this.module.ImportReference(returnType));
                    this.method.HasThis = false;

                    foreach (var parameterToken in tokens.Skip(3))
                    {
                        var splitted = parameterToken.Text.Split(':');
                        if (splitted.Length >= 3)
                        {
                            this.OutputError(
                                parameterToken,
                                $"Invalid parameter: {parameterToken.Text}");
                        }
                        else
                        {
                            var parameterTypeName = splitted.Last();
                            if (this.TryGetType(parameterTypeName, out var parameterType))
                            {
                                if (splitted.Length == 2)
                                {
                                    var parameterName = splitted[0];
                                    this.method.Parameters.Add(
                                        new ParameterDefinition(
                                            parameterName,
                                            ParameterAttributes.None,
                                            parameterType));
                                }
                                else
                                {
                                    this.method.Parameters.Add(
                                        new ParameterDefinition(parameterType));
                                }
                            }
                            else
                            {
                                this.OutputError(
                                    parameterToken,
                                    $"Invalid parameter: {parameterToken.Text}");
                            }
                        }
                    }

                    this.cabiSpecificModuleType.Methods.Add(this.method);

                    this.body = this.method.Body;
                    this.body.InitLocals = false;   // Derived C behavior.

                    this.instructions = this.body.Instructions;

                    if (this.produceExecutable &&
                        functionName == "main")
                    {
                        this.module.EntryPoint = method;
                    }
                }
                break;
            // Global variable directive:
            case "global":
                if (tokens.Length <= 2)
                {
                    this.OutputError(directive, $"Missing global variable operand.");
                }
                else if (!this.TryGetType(tokens[1].Text, out var globalType))
                {
                    this.OutputError(
                        tokens[1],
                        $"Invalid global variable type name: {tokens[1].Text}");
                }
                else
                {
                    this.FinishCurrentFunction();

                    var globalName = tokens[2].Text;
                    var field = new FieldDefinition(
                        globalName,
                        FieldAttributes.Public | FieldAttributes.Static,
                        this.module.ImportReference(globalType));

                    // TODO: initializer

                    this.cabiSpecificModuleType.Fields.Add(field);
                }
                break;
            // Local variable directive:
            case "local":
                if (this.instructions == null)
                {
                    this.OutputError(
                        directive,
                        $"Function directive is not defined.");
                }
                else if (tokens.Length >= 4)
                {
                    this.OutputError(
                        directive,
                        $"Too many operands.");
                }
                else
                {
                    var localTypeName = tokens[1].Text;
                    if (this.knownTypes.TryGetValue(
                        localTypeName,
                        out var localType))
                    {
                        var variable = new VariableDefinition(
                            this.module.ImportReference(localType));
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
                    else
                    {
                        this.OutputError(
                            tokens[1],
                            $"Invalid local variable type name: {localTypeName}");
                    }
                }
                break;
            // Location directive:
            case "location":
                if (this.instructions == null)
                {
                    this.OutputError(
                        directive,
                        $"Function directive is not defined.");
                }
                else if (this.produceDebuggingInformation)
                {
                    if (tokens.Length <= 1)
                    {
                        this.OutputError(
                            directive,
                            $"Missing location operand.");
                    }
                    else if (!int.TryParse(tokens[1].Text, out var lineIndex))
                    {
                        this.OutputError(
                            directive,
                            $"Invalid operand: {tokens[1].Text}");
                    }
                    else
                    {
                        switch (tokens.Length)
                        {
                            // Only line index:
                            case 2:
                                // (1 based index)
                                this.queuedLocation = new(
                                    this.relativePath, lineIndex - 1, 0, lineIndex - 1, 255, null);
                                this.isProducedOriginalSourceCodeLocation = false;
                                break;
                            case 3:
                                this.OutputError(
                                    directive,
                                    $"Missing operand.");
                                break;
                            // Line index, relative path and language:
                            case 4:
                                if (Utilities.TryParseEnum<DocumentLanguage>(tokens[3].Text, out var language))
                                {
                                    // (1 based index)
                                    this.relativePath = tokens[2].Text;
                                    this.queuedLocation = new(
                                        this.relativePath, lineIndex - 1, 0, lineIndex - 1, 255, null);
                                    this.isProducedOriginalSourceCodeLocation = false;
                                }
                                else
                                {
                                    this.OutputError(
                                        tokens[3], $"Invalid language operand: {tokens[3].Text}");
                                }
                                break;
                            default:
                                this.OutputError(
                                    directive,
                                    $"Too many operands.");
                                break;
                        }
                    }
                }
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
