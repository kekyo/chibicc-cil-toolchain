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
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace chibias.Internal;

internal sealed partial class Parser
{
    private readonly struct Location
    {
        public readonly string RelativePath;
        public readonly int StartLine;
        public readonly int StartColumn;
        public readonly int EndLine;
        public readonly int EndColumn;
        public readonly DocumentLanguage? Language;

        public Location(
            string relativePath,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            DocumentLanguage? language)
        {
            this.RelativePath = relativePath;
            this.StartLine = startLine;
            this.StartColumn = startColumn;
            this.EndLine = endLine;
            this.EndColumn = endColumn;
            this.Language = language;
        }
    }

    /////////////////////////////////////////////////////////////////////

    private readonly ILogger logger;
    private readonly ModuleDefinition module;
    private readonly TypeDefinition cabiSpecificModuleType;
    private readonly Dictionary<string, IMemberDefinition> cabiSpecificSymbols;
    private readonly bool produceExecutable;
    private readonly bool produceDebuggingInformation;
    private readonly Dictionary<string, TypeReference> knownTypes = new();
    private readonly Dictionary<string, Instruction> labelTargets = new();
    private readonly Dictionary<Instruction, Location> locationByInstructions = new();
    private readonly List<string> willApplyLabelingNames = new();
    private readonly List<Action> delayedLookupBranchTargetActions = new();
    private readonly List<Action> delayedLookupLocalMemberActions = new();
    private readonly Dictionary<string, List<VariableDebugInformation>> variableDebugInformationLists = new();

    private string relativePath = "unknown.s";
    private Location? queuedLocation;
    private Location? lastLocation;
    private bool isProducedOriginalSourceCodeLocation = true;
    private MethodDefinition? method;
    private MethodBody? body;
    private ICollection<Instruction>? instructions;
    private bool caughtError;

    /////////////////////////////////////////////////////////////////////

    public Parser(
        ILogger logger,
        ModuleDefinition module,
        TypeDefinition cabiSpecificModuleType,
        Dictionary<string, IMemberDefinition> cabiSpecificSymbols,
        bool produceExecutable,
        bool produceDebuggingInformation)
    {
        this.logger = logger;
        this.module = module;
        this.cabiSpecificModuleType = cabiSpecificModuleType;
        this.cabiSpecificSymbols = cabiSpecificSymbols;
        this.produceExecutable = produceExecutable;
        this.produceDebuggingInformation = produceDebuggingInformation;

        // Known types
        this.knownTypes.Add("void", module.TypeSystem.Void);
        this.knownTypes.Add("uint8", module.TypeSystem.Byte);
        this.knownTypes.Add("int8", module.TypeSystem.SByte);
        this.knownTypes.Add("int16", module.TypeSystem.Int16);
        this.knownTypes.Add("uint16", module.TypeSystem.UInt16);
        this.knownTypes.Add("int32", module.TypeSystem.Int32);
        this.knownTypes.Add("uint32", module.TypeSystem.UInt32);
        this.knownTypes.Add("int64", module.TypeSystem.Int64);
        this.knownTypes.Add("uint64", module.TypeSystem.UInt64);
        this.knownTypes.Add("float32", module.TypeSystem.Single);
        this.knownTypes.Add("float64", module.TypeSystem.Double);
        this.knownTypes.Add("intptr", module.TypeSystem.IntPtr);
        this.knownTypes.Add("uintptr", module.TypeSystem.UIntPtr);
        this.knownTypes.Add("bool", module.TypeSystem.Boolean);
        this.knownTypes.Add("char", module.TypeSystem.Char);
        this.knownTypes.Add("object", module.TypeSystem.Object);
        this.knownTypes.Add("string", module.TypeSystem.String);
        this.knownTypes.Add("typeref", module.TypeSystem.TypedReference);

        // Aliases
        this.knownTypes.Add("byte", module.TypeSystem.Byte);
        this.knownTypes.Add("sbyte", module.TypeSystem.SByte);
        this.knownTypes.Add("short", module.TypeSystem.Int16);
        this.knownTypes.Add("ushort", module.TypeSystem.UInt16);
        this.knownTypes.Add("int", module.TypeSystem.Int32);
        this.knownTypes.Add("uint", module.TypeSystem.UInt32);
        this.knownTypes.Add("long", module.TypeSystem.Int64);
        this.knownTypes.Add("ulong", module.TypeSystem.UInt64);
        this.knownTypes.Add("single", module.TypeSystem.Single);
        this.knownTypes.Add("float", module.TypeSystem.Single);
        this.knownTypes.Add("double", module.TypeSystem.Double);
        this.knownTypes.Add("nint", module.TypeSystem.IntPtr);
        this.knownTypes.Add("nuint", module.TypeSystem.UIntPtr);
        this.knownTypes.Add("char16", module.TypeSystem.Char);
    }

    /////////////////////////////////////////////////////////////////////

    public void SetSourceFile(string relativePath)
    {
        this.relativePath = relativePath;
        this.isProducedOriginalSourceCodeLocation = true;
        this.queuedLocation = null;
        this.lastLocation = null;
    }

    /////////////////////////////////////////////////////////////////////

    private void OutputError(Token token, string message)
    {
        this.caughtError = true;
        this.logger.Error($"{this.relativePath}({token.Line + 1},{token.StartColumn + 1}): {message}");
    }

    private void OutputError(Location location, string message)
    {
        this.caughtError = true;
        this.logger.Error($"{location.RelativePath}({location.StartLine + 1},{location.StartColumn + 1}): {message}");
    }

    private void OutputTrace(string message) =>
        this.logger.Trace($"{message}");

    private bool TryGetType(string typeName, out TypeReference type)
    {
        switch (typeName[typeName.Length - 1])
        {
            case '*':
                if (this.TryGetType(typeName.Substring(0, typeName.Length - 1), out var preType1))
                {
                    type = new PointerType(
                        this.module.ImportReference(preType1));
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
            case '&':
                if (this.TryGetType(typeName.Substring(0, typeName.Length - 1), out var preType2))
                {
                    type = new ByReferenceType(
                        this.module.ImportReference(preType2));
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
            case ']' when typeName.Length >= 2 && typeName[typeName.Length - 2] == '[':
                if (this.TryGetType(typeName.Substring(0, typeName.Length - 2), out var preType3))
                {
                    type = new ArrayType(
                        this.module.ImportReference(preType3));
                    return true;
                }
                else
                {
                    type = null!;
                    return false;
                }
            default:
                return this.knownTypes.TryGetValue(typeName, out type!);
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void ParseLabel(Token token)
    {
        if (this.instructions == null)
        {
            this.OutputError(token, $"Function directive is not defined.");
        }
        else
        {
            this.willApplyLabelingNames.Add(token.Text);
        }
    }

    public void Parse(Token[] tokens)
    {
        if (tokens.FirstOrDefault() is { } token0)
        {
            switch (token0.Type)
            {
                // Is it assembler directive?
                case TokenTypes.Directive:
                    this.ParseDirective(token0, tokens);
                    break;
                // Is it label?
                case TokenTypes.Label:
                    this.ParseLabel(token0);
                    break;
                // Is it OpCode?
                case TokenTypes.Identity when Utilities.TryParseOpCode(token0.Text, out var opCode):
                    this.ParseInstruction(opCode, tokens);
                    break;
                // Other, invalid syntax.
                default:
                    this.OutputError(token0, $"Invalid syntax.");
                    break;
            }
        }
    }

    /////////////////////////////////////////////////////////////////////

    private void FinishCurrentFunction()
    {
        if (this.method != null)
        {
            Debug.Assert(this.instructions != null);
            Debug.Assert(this.body != null);

            if (!this.caughtError)
            {
                foreach (var action in this.delayedLookupBranchTargetActions)
                {
                    action();
                }
            }

            this.delayedLookupBranchTargetActions.Clear();
            this.labelTargets.Clear();
            this.willApplyLabelingNames.Clear();
            this.instructions = null;
            this.body = null;
            this.method = null;
        }
    }

    public bool Finish(bool applyOptimization)
    {
        this.FinishCurrentFunction();

        if (!this.caughtError)
        {
            foreach (var action in this.delayedLookupLocalMemberActions)
            {
                action();
            }

            // (Completed all CIL implementations in this place.)

            var documents = new Dictionary<string, Document>();
            foreach (var method in this.cabiSpecificModuleType.Methods)
            {
                if (method.Body.Instructions.Count >= 1)
                {
                    // Apply optimization.
                    if (applyOptimization)
                    {
                        method.Body.Optimize();
                    }

                    // After optimization, the instructions maybe changed absolute layouts,
                    // so we could set debugging information scope after that.
                    if (this.produceDebuggingInformation)
                    {
                        method.DebugInformation.Scope = new ScopeDebugInformation(
                            method.Body.Instructions.First(),
                            method.Body.Instructions.Last());

                        // Will make sequence points:
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if (this.locationByInstructions.TryGetValue(instruction, out var location))
                            {
                                if (!documents.TryGetValue(location.RelativePath, out var document))
                                {
                                    document = new(location.RelativePath);
                                    document.Type = DocumentType.Text;
                                    if (location.Language is { } language)
                                    {
                                        document.Language = language;
                                    }
                                    documents.Add(location.RelativePath, document);
                                }

                                var sequencePoint = new SequencePoint(
                                    instruction, document);

                                sequencePoint.StartLine = location.StartLine + 1;
                                sequencePoint.StartColumn = location.StartColumn + 1;
                                sequencePoint.EndLine = location.EndLine + 1;
                                sequencePoint.EndColumn = location.EndColumn + 1;

                                method.DebugInformation.SequencePoints.Add(
                                    sequencePoint);
                            }
                        }

                        // Will make local variable naming.
                        if (this.variableDebugInformationLists.TryGetValue(method.Name, out var list))
                        {
                            foreach (var variableDebugInformation in list)
                            {
                                method.DebugInformation.Scope.Variables.Add(
                                    variableDebugInformation);
                            }
                        }
                    }
                }
            }
        }

        this.delayedLookupLocalMemberActions.Clear();
        this.locationByInstructions.Clear();
        this.variableDebugInformationLists.Clear();

        this.isProducedOriginalSourceCodeLocation = true;
        this.relativePath = "unknown.s";
        this.queuedLocation = null;
        this.lastLocation = null;

        var finished = !this.caughtError;
        this.caughtError = false;

        return finished;
    }
}
