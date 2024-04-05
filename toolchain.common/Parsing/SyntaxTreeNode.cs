/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Parsing;

/////////////////////////////////////////////////////////////////////

public abstract class Node
{
    public readonly Token Token;

    protected Node(Token token) =>
        this.Token = token;
}

/////////////////////////////////////////////////////////////////////

public sealed class IdentityNode : Node
{
    public string Identity =>
        this.Token.Text;

    public IdentityNode(
        Token token) :
        base(token)
    {
    }
}

public sealed class BooleanNode : Node
{
    public readonly bool Value;

    public BooleanNode(
        bool value,
        Token token) :
        base(token) =>
        this.Value = value;
}

public sealed class NumericNode : Node
{
    public readonly object Value;

    public NumericNode(
        object value,
        Token token) :
        base(token) =>
        this.Value = value;
}

public sealed class StringNode : Node
{
    public string Text =>
        this.Token.Text;

    public StringNode(
        Token token) :
        base(token)
    {
    }
}

/////////////////////////////////////////////////////////////////////

public abstract class DeclarationNode : Node
{
    protected DeclarationNode(Token token) :
        base(token)
    {
    }
}

/////////////////////////////////////////////////////////////////////

public enum Scopes
{
    Public,
    Internal,
    File,
    _Module_,   // For internal use only.
}

public sealed class ScopeDescriptorNode : Node
{
    public readonly Scopes Scope;

    public ScopeDescriptorNode(
        Scopes scope,
        Token token) :
        base(token) =>
        this.Scope = scope;
}

/////////////////////////////////////////////////////////////////////

public sealed class LocalVariableNode : Node
{
    public readonly TypeNode Type;
    public readonly IdentityNode? Name;

    public LocalVariableNode(
        TypeNode type,
        IdentityNode? name,
        Token token) :
        base(token)
    {
        this.Type = type;
        this.Name = name;
    }
}

/////////////////////////////////////////////////////////////////////

public abstract class LogicalInstruction
{
    public readonly Location? Location;

    protected LogicalInstruction(Location? location) =>
        this.Location = location;
}

public sealed class Label : LogicalInstruction
{
    public readonly IdentityNode Name;

    public Label(
        IdentityNode name,
        Location? location) :
        base(location) =>
        this.Name = name;
}

public abstract class Instruction : LogicalInstruction
{
    public readonly IdentityNode OpCode;

    protected Instruction(
        IdentityNode opCode,
        Location? location) :
        base(location) =>
        this.OpCode = opCode;
}

public sealed class SingleInstruction : Instruction
{
    public SingleInstruction(
        IdentityNode opCode,
        Location? location) :
        base(opCode, location)
    {
    }
}

public sealed class BranchInstruction : Instruction
{
    public readonly IdentityNode Label;

    public BranchInstruction(
        IdentityNode opCode,
        IdentityNode label,
        Location? location) :
        base(opCode, location) =>
        this.Label = label;
}

public sealed class FieldInstruction : Instruction
{
    public readonly IdentityNode Field;

    public FieldInstruction(
        IdentityNode opCode,
        IdentityNode field,
        Location? location) :
        base(opCode, location) =>
        this.Field = field;
}

public sealed class TypeInstruction : Instruction
{
    public readonly TypeNode Type;

    public TypeInstruction(
        IdentityNode opCode,
        TypeNode type,
        Location? location) :
        base(opCode, location) =>
        this.Type = type;
}

public sealed class MetadataTokenInstruction : Instruction
{
    public readonly IdentityNode Identity;

    public MetadataTokenInstruction(
        IdentityNode opCode,
        IdentityNode identity,
        Location? location) :
        base(opCode, location) =>
        this.Identity = identity;
}

public sealed class NumericValueInstruction : Instruction
{
    public readonly NumericNode Value;

    public NumericValueInstruction(
        IdentityNode opCode,
        NumericNode value,
        Location? location) :
        base(opCode, location) =>
        this.Value = value;
}

public sealed class StringValueInstruction : Instruction
{
    public readonly StringNode Value;

    public StringValueInstruction(
        IdentityNode opCode,
        StringNode value,
        Location? location) :
        base(opCode, location) =>
        this.Value = value;
}

public abstract class VariableInstruction : Instruction
{
    public VariableInstruction(
        IdentityNode opCode,
        Location? location) :
        base(opCode, location)
    {
    }
}

public sealed class VariableIndexInstruction : VariableInstruction
{
    public readonly NumericNode Index;

    public VariableIndexInstruction(
        IdentityNode opCode,
        NumericNode index,
        Location? location) :
        base(opCode, location) =>
        this.Index = index;
}

public sealed class VariableNameInstruction : VariableInstruction
{
    public readonly IdentityNode Name;

    public VariableNameInstruction(
        IdentityNode opCode,
        IdentityNode name,
        Location? location) :
        base(opCode, location) =>
        this.Name = name;
}

public sealed class CallInstruction : Instruction
{
    public readonly IdentityNode Function;
    public readonly FunctionSignatureNode? Signature;

    public CallInstruction(
        IdentityNode opCode,
        IdentityNode function,
        FunctionSignatureNode? signature,
        Location? location) :
        base(opCode, location)
    {
        this.Function = function;
        this.Signature = signature;
    }
}

public sealed class SignatureInstruction : Instruction
{
    public readonly FunctionSignatureNode? Signature;

    public SignatureInstruction(
        IdentityNode opCode,
        FunctionSignatureNode? signature,
        Location? location) :
        base(opCode, location) =>
        this.Signature = signature;
}

/////////////////////////////////////////////////////////////////////

public abstract class FunctionDescriptorNode : DeclarationNode
{
    public readonly ScopeDescriptorNode Scope;
    public readonly LocalVariableNode[] LocalVariables;
    public readonly LogicalInstruction[] Instructions;

    protected FunctionDescriptorNode(
        ScopeDescriptorNode scope,
        LocalVariableNode[] localVariables,
        LogicalInstruction[] instructions,
        Token token) :
        base(token)
    {
        this.Scope = scope;
        this.LocalVariables = localVariables;
        this.Instructions = instructions;
    }
}

public sealed class FunctionNode : FunctionDescriptorNode
{
    public readonly IdentityNode Name;
    public readonly FunctionSignatureNode Signature;

    public FunctionNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        FunctionSignatureNode signature,
        LocalVariableNode[] localVariables,
        LogicalInstruction[] instructions,
        Token token) :
        base(scope, localVariables, instructions, token)
    {
        this.Name = name;
        this.Signature = signature;
    }
}

public sealed class InitializerNode : FunctionDescriptorNode
{
    public InitializerNode(
        ScopeDescriptorNode scope,
        LocalVariableNode[] localVariables,
        LogicalInstruction[] instructions,
        Token token) :
        base(scope, localVariables, instructions, token)
    {
    }
}

/////////////////////////////////////////////////////////////////////

public sealed class InitializingDataNode : Node
{
    public readonly byte[] Data;

    public InitializingDataNode(
        byte[] data,
        Token token) :
        base(token) =>
        this.Data = data;
}

public abstract class VariableNode : DeclarationNode
{
    public readonly IdentityNode Name;
    public readonly ScopeDescriptorNode Scope;
    public readonly TypeNode Type;

    protected VariableNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        TypeNode type,
        Token token) :
        base(token)
    {
        this.Name = name;
        this.Scope = scope;
        this.Type = type;
    }
}

public sealed class GlobalVariableNode : VariableNode
{
    public readonly InitializingDataNode? InitializingData;

    public GlobalVariableNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        TypeNode type,
        InitializingDataNode? initializingData,
        Token token) :
        base(name, scope, type, token) =>
        this.InitializingData = initializingData;
}

public sealed class GlobalConstantNode : VariableNode
{
    public readonly InitializingDataNode InitializingData;

    public GlobalConstantNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        TypeNode type,
        InitializingDataNode initializingData,
        Token token) :
        base(name, scope, type, token) =>
        this.InitializingData = initializingData;
}

/////////////////////////////////////////////////////////////////////

public sealed class EnumerationValue
{
    public readonly IdentityNode Name;
    public readonly NumericNode? Value;

    public EnumerationValue(
        IdentityNode name,
        NumericNode? value)
    {
        this.Name = name;
        this.Value = value;
    }
}

public sealed class EnumerationNode : DeclarationNode
{
    public readonly IdentityNode Name;
    public readonly ScopeDescriptorNode Scope;
    public readonly TypeNode UnderlyingType;
    public readonly EnumerationValue[] Values;
    
    public EnumerationNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        TypeNode underlyingType,
        EnumerationValue[] values,
        Token token) :
        base(token)
    {
        this.Name = name;
        this.Scope = scope;
        this.UnderlyingType = underlyingType;
        this.Values = values;
    }
}

/////////////////////////////////////////////////////////////////////

public sealed class StructureField
{
    public readonly TypeNode Type;
    public readonly IdentityNode Name;
    public readonly NumericNode? Offset;

    public StructureField(
        TypeNode type,
        IdentityNode name,
        NumericNode? offset)
    {
        this.Type = type;
        this.Name = name;
        this.Offset = offset;
    }
}

public sealed class StructureNode : DeclarationNode
{
    public readonly IdentityNode Name;
    public readonly ScopeDescriptorNode Scope;
    public readonly BooleanNode? IsExplicit;
    public readonly NumericNode? PackSize;
    public readonly StructureField[] Fields;
    
    public StructureNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        BooleanNode? isExplicit,
        NumericNode? packSize,
        StructureField[] fields,
        Token token) :
        base(token)
    {
        this.Name = name;
        this.Scope = scope;
        this.IsExplicit = isExplicit;
        this.PackSize = packSize;
        this.Fields = fields;
    }
}
