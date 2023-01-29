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
    private MethodDefinition SetupFunctionBodyDirective(
        string functionName,
        TypeReference returnType,
        ParameterDefinition[] parameters,
        bool isPublic)
    {
        this.FinishCurrentFunction();

        this.method = new MethodDefinition(
            functionName,
            isPublic ?
                (MethodAttributes.Public | MethodAttributes.Static) :
                (MethodAttributes.Private | MethodAttributes.Static),
            this.module.ImportReference(returnType));
        this.method.HasThis = false;

        foreach (var parameter in parameters)
        {
            this.method.Parameters.Add(parameter);
        }

        this.cabiSpecificModuleType.Methods.Add(this.method);

        this.body = this.method.Body;
        this.body.InitLocals = false;   // Derived C behavior.

        this.instructions = this.body.Instructions;

        return this.method;
    }

    private void ParseFunctionDirective(
        Token directive, Token[] tokens)
    {
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
            var functionName = tokens[2].Text;
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
                        if (this.TryGetType(parameterTypeName, out var parameterType))
                        {
                            if (splitted.Length == 2)
                            {
                                var parameterName = splitted[0];
                                return new ParameterDefinition(
                                    parameterName,
                                    ParameterAttributes.None,
                                    parameterType);
                            }
                            else
                            {
                                return new ParameterDefinition(parameterType);
                            }
                        }
                        else
                        {
                            this.OutputError(
                                parameterToken,
                                $"Invalid parameter: {parameterToken.Text}");
                        }
                        return null;
                    }
                }).
                ToArray();

            this.SetupFunctionBodyDirective(
                functionName,
                returnType,
                parameters,
                true);
        }
    }

    private void ParseInitializerDirective(
        Token directive, Token[] tokens)
    {
        if (tokens.Length >= 2)
        {
            this.OutputError(directive, $"Too many operands.");
        }
        else
        {
            var functionName = $"<initializer>_${this.initializers.Count}";

            var initializer = this.SetupFunctionBodyDirective(
                functionName,
                this.module.TypeSystem.Void,
                Utilities.Empty<ParameterDefinition>(),
                false);

            this.initializers.Add(initializer);
        }
    }

    private void ParseGlobalDirective(
        Token directive, Token[] tokens)
    {
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

            this.cabiSpecificModuleType.Fields.Add(field);
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
        else if (tokens.Length >= 4)
        {
            this.OutputError(
                directive,
                $"Too many operands.");
        }
        else if (this.TryGetType(tokens[1].Text, out var localType))
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
                $"Invalid local variable type name: {tokens[1].Text}");
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
            this.FinishCurrentFunction();

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

            var dataName = tokens[1].Text;

            if (!this.constantTypes.TryGetValue(data.Length, out var constantType))
            {
                var constantTypeName = $"<constant_type>_${data.Length}";
                constantType = new TypeDefinition(
                    "",
                    constantTypeName,
                    TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout,
                    this.valueType.Value);
                constantType.PackingSize = 1;
                constantType.ClassSize = data.Length;

                this.cabiSpecificModuleType.NestedTypes.Add(constantType);
                this.constantTypes.Add(data.Length, constantType);
            }

            var field = new FieldDefinition(
                dataName,
                FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly,
                constantType);
            field.InitialValue = data;

            this.cabiSpecificModuleType.Fields.Add(field);
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
                                this.relativePath, lineIndex - 1, 0, lineIndex - 1, 255, language);
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
            // Constant directive:
            case "constant":
                this.ParseConstantDirective(directive, tokens);
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
