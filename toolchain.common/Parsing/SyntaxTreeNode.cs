/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System.Reflection.Emit;
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Parsing;

/////////////////////////////////////////////////////////////////////

public abstract class Node
{
    public readonly Token Token;

    protected Node(Token token) =>
        this.Token = token;
}

public abstract class ValueNode : Node
{
    protected ValueNode(
        Token token) :
        base(token)
    {
    }
}

public sealed class IdentityNode : ValueNode
{
    public string Identity =>
        this.Token.Text;

    public IdentityNode(
        Token token) :
        base(token)
    {
    }
}

public sealed class BooleanNode : ValueNode
{
    public readonly bool Value;

    public BooleanNode(
        bool value,
        Token token) :
        base(token) =>
        this.Value = value;
}

public sealed class NumericNode : ValueNode
{
    public readonly object Value;

    public NumericNode(
        object value,
        Token token) :
        base(token) =>
        this.Value = value;
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

public sealed class Instruction : LogicalInstruction
{
    public readonly IdentityNode OpCode;
    public readonly IdentityNode? Operand;

    public Instruction(
        IdentityNode opCode,
        IdentityNode? operand,
        Location? location) :
        base(location)
    {
        this.OpCode = opCode;
        this.Operand = operand;
    }
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
