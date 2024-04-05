/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Logging;
using chibicc.toolchain.Tokenizing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;

namespace chibicc.toolchain.Parsing;

public sealed class CilParser
{
    private static readonly Dictionary<string, OpCode> opCodes =
        typeof(OpCodes).GetFields().
            Where(field =>
                field.IsPublic && field.IsStatic && field.IsInitOnly &&
                field.FieldType.FullName == "System.Reflection.Emit.OpCode").
            Select(field => (OpCode)field.GetValue(null)!).
            ToDictionary(opCode => opCode.Name!.Replace('_', '.'), StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Func<string, object?>> toUnderlyingTypedValues =
        new()
        {
            { "System.Byte", str => byte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.Int16", str => short.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.Int32", str => int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.Int64", str => long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.SByte", str => sbyte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.UInt16", str => ushort.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.UInt32", str => uint.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
            { "System.UInt64", str => ulong.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null },
        };
    
    private readonly ILogger logger;
    private readonly Dictionary<string, FileDescriptor> files = new();

    private Location? currentLocation;
    private bool caughtError;

    public CilParser(ILogger logger)
    {
        this.logger = logger;
    }

    public bool CaughtError =>
        this.caughtError;

    /////////////////////////////////////////////////////////////////////

    private void OutputError(Token token, string message)
    {
        this.caughtError = true;
        this.logger.Error(
            $"{token.RelativePath}:{token.Line + 1}:{token.StartColumn + 1}: {message}");
    }

    private void OutputWarning(Token token, string message)
    {
        this.caughtError = true;
        this.logger.Warning(
            $"{token.RelativePath}:{token.Line + 1}:{token.StartColumn + 1}: {message}");
    }

    /////////////////////////////////////////////////////////////////////

    private static bool TryLookupScopeDescriptorName(
        Token token,
        out ScopeDescriptorNode scope)
    {
        if (!Enum.TryParse<Scopes>(token.Text, true, out var sd))
        {
            scope = null!;
            return false;
        }
        scope = new(sd, token);
        return true;
    }

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

        if (localTypeNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                localTypeNameToken,
                $"Invalid local variable type name: {localTypeNameToken}");
            return null;
        }
        
        if (!TypeParser.TryParse(localTypeNameToken, out var localType))
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
                    if (tokens.Length > 1)
                    {
                        this.OutputError(
                            tokens[1],
                            $"Too many operands: {tokens[1]}");
                        continue;
                    }
                    instructions.Add(new Label(
                        new(token0),
                        this.currentLocation));
                    continue;

                // Opcodes:
                case (TokenTypes.Identity, _):
                    if (tokens.Length > 2)
                    {
                        this.OutputError(
                            tokens[2],
                            $"Too many operands: {tokens[2]}");
                        continue;
                    }
                    if (!opCodes.TryGetValue(token0.Text, out var opCode))
                    {
                        this.OutputError(
                            token0,
                            $"Invalid opcode: {token0}");
                        continue;
                    }
                    if (opCode.OperandType == OperandType.InlineNone &&
                        tokens.Length == 2)
                    {
                        this.OutputError(
                            tokens[1],
                            $"Invalid operand: {tokens[1]}");
                        continue;
                    }
                    if (opCode.OperandType != OperandType.InlineNone &&
                        tokens.Length == 1)
                    {
                        this.OutputError(
                            token0,
                            $"Missing operand: {token0}");
                        continue;
                    }
                    instructions.Add(new Instruction(
                        new(token0),
                        tokens.Length == 2 ? new IdentityNode(tokens[1]) : null,
                        this.currentLocation));
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

    /////////////////////////////////////////////////////////////////////

    private FunctionNode? ParseFunctionDirective(
        TokensIterator tokensIterator, Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing directive operands.");
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
            out var scope))
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var functionSignatureToken = tokens[2];

        if (!TypeParser.TryParse(functionSignatureToken, out var rootTypeNode))
        {
            this.OutputError(
                functionSignatureToken,
                $"Invalid function signature: {functionSignatureToken}");
            return null;
        }

        if (!(rootTypeNode is FunctionSignatureNode fsn))
        {
            this.OutputError(
                functionSignatureToken,
                $"Invalid function signature: {functionSignatureToken}");
            return null;
        }
        
        var functionNameToken = tokens[3];

        if (functionNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                functionSignatureToken,
                $"Invalid function name: {functionNameToken}");
            return null;
        }

        var (localVariables, instructions) = this.ParseFunctionBody(
            tokensIterator);
        
        return new FunctionNode(
            new(functionNameToken),
            scope,
            fsn,
            localVariables,
            instructions,
            tokens[0]);
    }

    /////////////////////////////////////////////////////////////////////

    private InitializerNode? ParseInitializerDirective(
        TokensIterator tokensIterator, Token[] tokens)
    {
        if (tokens.Length < 2)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing directive operands.");
            return null;
        }

        if (tokens.Length > 2)
        {
            this.OutputError(
                tokens[2],
                $"Too many operands: {tokens[2]}");
            return null;
        }

        var scopeToken = tokens[1];
        
        if (!TryLookupScopeDescriptorName(
            scopeToken,
            out var scope) ||
            scope.Scope == Scopes.Public)
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var (localVariables, instructions) = this.ParseFunctionBody(
            tokensIterator);

        return new InitializerNode(
            scope,
            localVariables,
            instructions,
            tokens[0]);
    }

    /////////////////////////////////////////////////////////////////////

    private VariableNode? ParseVariableDirective(
        Token[] tokens, bool isConstant)
    {
        var valueTypeDisplayName = isConstant ? "constant" : "global variable";
        
        // Constant directive requires initializing data.
        if (tokens.Length < (isConstant ? 5 : 4))
        {
            this.OutputError(
                tokens.Last(),
                $"Missing {valueTypeDisplayName} operand.");
            return null;
        }

        var scopeToken = tokens[1];
        
        if (!TryLookupScopeDescriptorName(
            scopeToken,
            out var scope))
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var globalTypeNameToken = tokens[2];

        if (!TypeParser.TryParse(
            globalTypeNameToken,
            out var globalType))
        {
            this.OutputError(
                globalTypeNameToken,
                $"Invalid {valueTypeDisplayName} type name: {globalTypeNameToken}");
            return null;
        }

        var valueNameToken = tokens[3];

        if (valueNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                valueNameToken,
                $"Invalid {valueTypeDisplayName} name: {valueNameToken}");
            return null;
        }

        InitializingDataNode? initializeData = null;
        if (tokens.Length >= 5)
        {
            initializeData = new(
                tokens.
                    Skip(4).
                    Select(token =>
                    {
                        if (token.Type != TokenTypes.Identity)
                        {
                            this.OutputError(
                                token,
                                $"Invalid data operand: {token}");
                            return (byte)0;
                        }
                        if (!byte.TryParse(
                            token.Text,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var value))
                        {
                            this.OutputError(
                                token,
                                $"Invalid data operand: {token}");
                            return (byte)0;
                        }
                        return value;
                    }).
                    ToArray(),
                tokens[4]);
        }
        
        Debug.Assert(!isConstant || initializeData != null);

        return isConstant ?
            new GlobalConstantNode(
                new(valueNameToken),
                scope,
                globalType,
                initializeData!,
                tokens[0]) :
            new GlobalVariableNode(
                new(valueNameToken),
                scope,
                globalType,
                initializeData,
                tokens[0]);
    }

    /////////////////////////////////////////////////////////////////////

    private EnumerationValue[] ParseEnumerationValues(
        TokensIterator tokensIterator, string underlyingTypeName)
    {
        var enumerationValues = new List<EnumerationValue>();

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
                        if (valueToken.Type != TokenTypes.Identity)
                        {
                            this.OutputError(
                                tokens[2],
                                $"Invalid value: {valueToken}");
                            continue;
                        }
                        if (!toUnderlyingTypedValues.TryGetValue(underlyingTypeName, out var f) ||
                            f(valueToken.Text) is not { } v)
                        {
                            this.OutputError(
                                tokens[2],
                                $"Invalid value: {valueToken}");
                            continue;
                        }
                        enumerationValues.Add(new(
                            new(token0),
                            new(v, valueToken)));
                    }
                    else
                    {
                        enumerationValues.Add(new(
                            new(token0),
                            null));
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
        TokensIterator tokensIterator, Token[] tokens)
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
            out var scope))
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        var underlyingTypeNameToken = tokens[2];

        if (!TypeParser.TryParse(underlyingTypeNameToken, out var underlyingType) ||
            underlyingType is not TypeIdentityNode(var underlyingTypeName) ||
            TypeParser.IsEnumerationUnderlyingType(underlyingTypeName))
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
            tokensIterator, underlyingTypeName);

        return new(
            new(enumerationNameToken),
            scope,
            underlyingType,
            enumerationValues,
            tokens[0]);
    }

    /////////////////////////////////////////////////////////////////////

    private StructureField[] ParseStructureFields(
        TokensIterator tokensIterator, bool isExplicit)
    {
        var structureFields = new List<StructureField>();

        while (tokensIterator.TryGetNext(out var tokens))
        {
            var token0 = tokens[0];
            switch (token0)
            {
                // Structure member:
                case (TokenTypes.Identity, _):
                    if (tokens.Length < 3)
                    {
                        this.OutputError(
                            tokens.Last(),
                            $"Missing member operand.");
                        continue;
                    }
                    if (tokens.Length > 4)
                    {
                        this.OutputError(
                            tokens[4],
                            $"Too many operands: {tokens[4]}");
                        continue;
                    }
                    var scopeToken = token0;
                    if (!TryLookupScopeDescriptorName(
                        scopeToken,
                        out var scope) ||
                        scope.Scope == Scopes.File)
                    {
                        this.OutputError(
                            scopeToken,
                            $"Invalid scope descriptor: {scopeToken}");
                        continue;
                    }
                    var memberTypeNameToken = tokens[1];
                    if (!TypeParser.TryParse(
                        memberTypeNameToken,
                        out var memberType))
                    {
                        this.OutputError(
                            memberTypeNameToken,
                            $"Invalid member type name: {memberTypeNameToken}");
                        continue;
                    }
                    var memberNameToken = tokens[2];
                    if (memberNameToken.Type != TokenTypes.Identity)
                    {
                        this.OutputError(
                            memberNameToken,
                            $"Invalid member name: {memberNameToken}");
                        continue;
                    }
                    int? memberOffset = null;
                    if (!isExplicit)
                    {
                        if (tokens.Length == 4)
                        {
                            this.OutputError(
                                tokens[3],
                                $"Could not apply member offset: {tokens[3]}");
                            continue;
                        }
                    }
                    else
                    {
                        if (tokens.Length == 3)
                        {
                            this.OutputError(
                                memberNameToken,
                                $"Missing member offset operand: {memberNameToken}");
                            continue;
                        }
                        if (!int.TryParse(
                            tokens[3].Text,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var offset) ||
                            offset < 0)
                        {
                            this.OutputError(
                                tokens[3],
                                $"Invalid member offset: {tokens[3].Text}");
                            continue;
                        }
                        memberOffset = offset;
                    }
                    structureFields.Add(new(
                        memberType,
                        new(memberNameToken),
                        memberOffset is { } mo ? new NumericNode(mo, tokens[3]) : null));
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

        return structureFields.ToArray();
    }

    private StructureNode? ParseStructureDirective(
        TokensIterator tokensIterator, Token[] tokens)
    {
        if (tokens.Length < 3)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing structure operand.");
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
            out var scope))
        {
            this.OutputError(
                scopeToken,
                $"Invalid scope descriptor: {scopeToken}");
            return null;
        }

        bool? isExplicit = null;
        short? packSize = null;
        var aligningToken = tokens.ElementAtOrDefault(3);

        if (aligningToken != null)
        {
            if (aligningToken.Type != TokenTypes.Identity)
            {
                this.OutputError(
                    aligningToken,
                    $"Invalid operand: {aligningToken}");
                return null;
            }
            
            var aligning = aligningToken.Text;
            if (aligning == "explicit")
            {
                isExplicit = true;
            }
            else if (short.TryParse(
                aligning,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ps1))
            {
                if (ps1 < 1)
                {
                    this.OutputError(
                        aligningToken,
                        $"Invalid pack size: {aligningToken}");
                    return null;
                }
                packSize = ps1;
            }
            else
            {
                this.OutputError(
                    aligningToken,
                    $"Invalid operand: {aligningToken}");
                return null;
            }
        }

        var structureNameToken = tokens[2];

        if (structureNameToken.Type != TokenTypes.Identity)
        {
            this.OutputError(
                structureNameToken,
                $"Invalid structure name: {structureNameToken}");
            return null;
        }

        var structureFields = this.ParseStructureFields(
            tokensIterator, isExplicit ?? false);

        return new(
            new(structureNameToken),
            scope,
            isExplicit is { } ie ? new BooleanNode(ie, aligningToken!) : null,
            packSize is { } ps2 ? new NumericNode(ps2, aligningToken!) : null,
            structureFields.ToArray(),
            tokens[0]);
    }
    
    /////////////////////////////////////////////////////////////////////

    private readonly struct FileDirectiveResults
    {
        public readonly string FileId;
        public readonly FileDescriptor File;

        public FileDirectiveResults(
            string fileId,
            FileDescriptor file)
        {
            this.FileId = fileId;
            this.File = file;
        }

        public void Deconstruct(
            out string fileId,
            out FileDescriptor file)
        {
            fileId = this.FileId;
            file = this.File;
        }
    }
    
    private FileDirectiveResults? ParseFileDirective(
        Token[] tokens)
    {
        if (tokens.Length < 4)
        {
            this.OutputError(
                tokens.Last(),
                $"Missing file operands.");
            return null;
        }

        if (tokens.Length > 4)
        {
            this.OutputError(
                tokens[4],
                $"Too many operands: {tokens[4]}");
            return null;
        }

        var languageToken = tokens[3];
        
        if (!Enum.TryParse<Language>(languageToken.Text, true, out var language))
        {
            this.OutputError(
                languageToken,
                $"Invalid language operand: {languageToken}");
            return null;
        }

        var fileId = tokens[1].Text;
        var path = tokens[2].Text;
        
        return new(
            fileId,
            new(Path.GetDirectoryName(path),
                Path.GetFileName(path),
                language,
                true));
    }
    
    /////////////////////////////////////////////////////////////////////

    public IEnumerable<DeclarationNode> Parse(
        IEnumerable<Token[]> tokenLists)
    {
        this.files.Clear();
        this.currentLocation = null;
        
        using var tokenIterator = new TokensIterator(tokenLists);
        
        var caughtSyntaxError = false;
        while (tokenIterator.TryGetNext(out var tokens))
        {
            Debug.Assert(tokens.Length >= 1);
            
            var token0 = tokens[0];
            switch (token0)
            {
                // Function directive:
                case (TokenTypes.Directive, "function"):
                    caughtSyntaxError = false;
                    if (this.ParseFunctionDirective(tokenIterator, tokens) is { } function)
                    {
                        yield return function;
                    }
                    break;
                        
                // Initializer directive:
                case (TokenTypes.Directive, "initializer"):
                    caughtSyntaxError = false;
                    if (this.ParseInitializerDirective(tokenIterator, tokens) is { } initializer)
                    {
                        yield return initializer;
                    }
                    break;

                // Global variable directive:
                case (TokenTypes.Directive, "global"):
                    caughtSyntaxError = false;
                    if (this.ParseVariableDirective(tokens, false) is { } global)
                    {
                        yield return global;
                    }
                    break;

                // Constant directive:
                case (TokenTypes.Directive, "constant"):
                    caughtSyntaxError = false;
                    if (this.ParseVariableDirective(tokens, true) is { } constant)
                    {
                        yield return constant;
                    }
                    break;
            
                // Enumeration directive:
                case (TokenTypes.Directive, "enumeration"):
                    caughtSyntaxError = false;
                    if (this.ParseEnumerationDirective(tokenIterator, tokens) is { } enumeration)
                    {
                        yield return enumeration;
                    }
                    break;
            
                // Structure directive:
                case (TokenTypes.Directive, "structure"):
                    caughtSyntaxError = false;
                    if (this.ParseStructureDirective(tokenIterator, tokens) is { } structure)
                    {
                        yield return structure;
                    }
                    break;
             
                // File directive:
                case (TokenTypes.Directive, "file"):
                    caughtSyntaxError = false;
                    if (this.ParseFileDirective(tokens) is var (fileId, file))
                    {
                        this.files[fileId] = file;
                    }
                    break;
               
                default:
                    // Sync any directives.
                    if (!caughtSyntaxError)
                    {
                        caughtSyntaxError = true;
                        this.OutputError(
                            token0,
                            $"Invalid syntax: {token0}");
                    }
                    break;
            }
        }
    }
}
