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
using System.IO;
using System.Linq;

namespace chibicc.toolchain.Parsing;

public sealed partial class CilParser
{
    private readonly ILogger logger;
    private readonly Dictionary<string, FileDescriptor> files = new();

    private Location? currentLocation;
    private bool caughtError;

    public CilParser(ILogger logger) =>
        this.logger = logger;

    /////////////////////////////////////////////////////////////////////

    public bool CaughtError =>
        this.caughtError;

    private void OutputError(Token token, string message)
    {
        this.caughtError = true;
        this.logger.Error(
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
        this.caughtError = false;
        
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
