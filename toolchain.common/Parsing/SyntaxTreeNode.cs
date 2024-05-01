/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Parsing;

/////////////////////////////////////////////////////////////////////

public abstract class Node :
    IEquatable<Node>
{
    protected Node()
    {
    }

    public abstract bool Equals(Node? rhs);

    public override bool Equals(object? obj) =>
        obj is Node r &&
        this.Equals(r);

    public override int GetHashCode() =>
        throw new NotImplementedException();

    protected static int CalculateHashCode<T>(IEnumerable<T> enumerable)
        where T : notnull =>
        enumerable.Aggregate(0, (agg, v) => v.GetHashCode());
}

public abstract class TokenNode : Node
{
    public readonly Token Token;

    protected TokenNode(Token token) =>
        this.Token = token;
    
    public void Deconstruct(
        out Token token) =>
        token = this.Token;
}

/////////////////////////////////////////////////////////////////////

public sealed class IdentityNode : TokenNode
{
    public string Identity =>
        this.Token.Text;

    public IdentityNode(
        Token token) :
        base(token)
    {
    }

    public override bool Equals(Node? rhs) =>
        rhs is IdentityNode r &&
        this.Identity.Equals(r.Identity);

    public override int GetHashCode() =>
        this.Identity.GetHashCode();
    
    public void Deconstruct(
        out string identity,
        out Token token)
    {
        identity = this.Identity;
        token = this.Token;
    }

    public override string ToString() =>
        this.Identity;

    public static IdentityNode Create(string text) =>
        new(Token.Identity(text));
}

public sealed class BooleanNode : TokenNode
{
    public readonly bool Value;

    public override bool Equals(Node? rhs) =>
        rhs is BooleanNode r &&
        this.Value.Equals(r.Value);

    public override int GetHashCode() =>
        this.Value.GetHashCode();

    public BooleanNode(
        bool value,
        Token token) :
        base(token) =>
        this.Value = value;

    public void Deconstruct(
        out bool value,
        out Token token)
    {
        value = this.Value;
        token = this.Token;
    }

    public override string ToString() =>
        this.Value ? "true" : "false";
}

public sealed class NumericNode : TokenNode
{
    public readonly object Value;

    public NumericNode(
        object value,
        Token token) :
        base(token) =>
        this.Value = value;

    public override bool Equals(Node? rhs) =>
        rhs is NumericNode r &&
        this.Value.Equals(r.Value);

    public override int GetHashCode() =>
        this.Value.GetHashCode();

    public void Deconstruct(
        out object value,
        out Token token)
    {
        value = this.Value;
        token = this.Token;
    }

    public override string ToString() =>
        this.Value.ToString()!;
}

public sealed class StringNode : TokenNode
{
    public string Text =>
        this.Token.Text;

    public StringNode(
        Token token) :
        base(token)
    {
    }

    public override bool Equals(Node? rhs) =>
        rhs is StringNode r &&
        this.Text.Equals(r.Text);

    public override int GetHashCode() =>
        this.Text.GetHashCode();

    public void Deconstruct(
        out string text,
        out Token token)
    {
        text = this.Text;
        token = this.Token;
    }

    public override string ToString() =>
        $"\"{this.Text}\"";
}

/////////////////////////////////////////////////////////////////////

public enum Scopes
{
    Public,
    Internal,
    File,
    [EditorBrowsable(EditorBrowsableState.Never)]
    __Module__,
}

public sealed class ScopeDescriptorNode : TokenNode
{
    public readonly Scopes Scope;

    public ScopeDescriptorNode(
        Scopes scope,
        Token token) :
        base(token) =>
        this.Scope = scope;

    public override bool Equals(Node? rhs) =>
        rhs is ScopeDescriptorNode r &&
        this.Scope.Equals(r.Scope);

    public override int GetHashCode() =>
        this.Scope.GetHashCode();

    public void Deconstruct(
        out Scopes scope,
        out Token token)
    {
        scope = this.Scope;
        token = this.Token;
    }

    public override string ToString() =>
        this.Scope.ToString().ToLowerInvariant();
}

/////////////////////////////////////////////////////////////////////

public sealed class LocalVariableNode : Node
{
    public readonly TypeNode Type;
    public readonly IdentityNode? Name;

    public LocalVariableNode(
        TypeNode type,
        IdentityNode? name)
    {
        this.Type = type;
        this.Name = name;
    }

    public override bool Equals(Node? rhs) =>
        rhs is LocalVariableNode r &&
        this.Type.Equals(r.Type) &&
        (this.Name?.Equals(r.Name) ?? r.Name == null);

    public override int GetHashCode() =>
        this.Type.GetHashCode() ^
        this.Name?.GetHashCode() ?? 0;

    public void Deconstruct(
        out TypeNode type,
        out IdentityNode? name)
    {
        type = this.Type;
        name = this.Name;
    }

    public override string ToString() =>
        this.Name is { } name ? $"{name}:{this.Type}" : $"{this.Type}";
}

/////////////////////////////////////////////////////////////////////

public sealed class LabelNode : TokenNode
{
    public readonly string Name;

    public LabelNode(
        string name,
        Token token) :
        base(token) =>
        this.Name = name;

    public override bool Equals(Node? rhs) =>
        rhs is LabelNode r &&
        this.Name.Equals(r.Name);

    public override int GetHashCode() =>
        this.Name.GetHashCode();

    public void Deconstruct(
        out string name,
        out Token token)
    {
        name = this.Name;
        token = this.Token;
    }

    public override string ToString() =>
        $"{this.Name}:";
}

public abstract class InstructionNode : Node
{
    public readonly IdentityNode OpCode;
    public readonly LabelNode[] Labels;
    public readonly Location? Location;

    protected InstructionNode(
        IdentityNode opCode,
        LabelNode[] labels,
        Location? location)
    {
        this.OpCode = opCode;
        this.Labels = labels;
        this.Location = location;
    }

    public override string ToString() =>
        string.Join(" ", (object[])this.Labels);
}

public sealed class SingleInstructionNode : InstructionNode
{
    public SingleInstructionNode(
        IdentityNode opCode,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location)
    {
    }
    
    public override bool Equals(Node? rhs) =>
        rhs is SingleInstructionNode r &&
        this.OpCode.Equals(r.OpCode);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode) =>
        opCode = this.OpCode;

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode}";
}

public sealed class BranchInstructionNode : InstructionNode
{
    public readonly IdentityNode LabelTarget;

    public BranchInstructionNode(
        IdentityNode opCode,
        IdentityNode labelTarget,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.LabelTarget = labelTarget;
    
    public override bool Equals(Node? rhs) =>
        rhs is BranchInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.LabelTarget.Equals(r.LabelTarget);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.LabelTarget.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out IdentityNode labelTarget)
    {
        opCode = this.OpCode;
        labelTarget = this.LabelTarget;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.LabelTarget.Identity}";
}

public sealed class FieldInstructionNode : InstructionNode
{
    public readonly IdentityNode Field;

    public FieldInstructionNode(
        IdentityNode opCode,
        IdentityNode field,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.Field = field;
    
    public override bool Equals(Node? rhs) =>
        rhs is FieldInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Field.Equals(r.Field);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Field.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out IdentityNode field)
    {
        opCode = this.OpCode;
        field = this.Field;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Field}";
}

public sealed class TypeInstructionNode : InstructionNode
{
    public readonly TypeNode Type;

    public TypeInstructionNode(
        IdentityNode opCode,
        TypeNode type,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.Type = type;
    
    public override bool Equals(Node? rhs) =>
        rhs is TypeInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Type.Equals(r.Type);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Type.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out TypeNode type)
    {
        opCode = this.OpCode;
        type = this.Type;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Type}";
}

public sealed class MetadataTokenInstructionNode : InstructionNode
{
    public readonly IdentityNode Identity;
    public readonly FunctionSignatureNode? Signature;

    public MetadataTokenInstructionNode(
        IdentityNode opCode,
        IdentityNode identity,
        FunctionSignatureNode? signature,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location)
    {
        this.Identity = identity;
        this.Signature = signature;
    }
    
    public override bool Equals(Node? rhs) =>
        rhs is MetadataTokenInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Identity.Equals(r.Identity) &&
        (this.Signature?.Equals(r.Signature) ?? r.Signature == null);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Identity.GetHashCode() ^
        this.Signature?.GetHashCode() ?? 0;

    public void Deconstruct(
        out IdentityNode opCode,
        out IdentityNode identity,
        out FunctionSignatureNode? signature)
    {
        opCode = this.OpCode;
        identity = this.Identity;
        signature = this.Signature;
    }

    public override string ToString() =>
        this.Signature is { } signature ?
            $"{base.ToString()} {this.OpCode} {signature} {this.Identity}" :
            $"{base.ToString()} {this.OpCode} {this.Identity}";
}

public sealed class NumericValueInstructionNode : InstructionNode
{
    public readonly NumericNode Value;

    public NumericValueInstructionNode(
        IdentityNode opCode,
        NumericNode value,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.Value = value;
    
    public override bool Equals(Node? rhs) =>
        rhs is NumericValueInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Value.Equals(r.Value);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Value.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out NumericNode value)
    {
        opCode = this.OpCode;
        value = this.Value;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Value}";
}

public sealed class StringValueInstructionNode : InstructionNode
{
    public readonly StringNode Value;

    public StringValueInstructionNode(
        IdentityNode opCode,
        StringNode value,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.Value = value;

    public override bool Equals(Node? rhs) =>
        rhs is StringValueInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Value.Equals(r.Value);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Value.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out StringNode value,
        out Location? location)
    {
        opCode = this.OpCode;
        value = this.Value;
        location = this.Location;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Value}";
}

public abstract class IndirectReferenceInstructionNode : InstructionNode
{
    public readonly bool IsArgumentIndirection;

    protected IndirectReferenceInstructionNode(
        IdentityNode opCode,
        bool isArgumentIndirection,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.IsArgumentIndirection = isArgumentIndirection;
}

public sealed class IndexReferenceInstructionNode : IndirectReferenceInstructionNode
{
    public readonly NumericNode Index;

    public IndexReferenceInstructionNode(
        IdentityNode opCode,
        bool isArgumentIndirection,
        NumericNode index,
        LabelNode[] labels,
        Location? location) :
        base(opCode, isArgumentIndirection, labels, location) =>
        this.Index = index;

    public override bool Equals(Node? rhs) =>
        rhs is IndexReferenceInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.IsArgumentIndirection.Equals(r.IsArgumentIndirection) &&
        this.Index.Equals(r.Index);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.IsArgumentIndirection.GetHashCode() ^
        this.Index.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out bool isArgumentIndirection,
        out NumericNode index)
    {
        opCode = this.OpCode;
        isArgumentIndirection = this.IsArgumentIndirection;
        index = this.Index;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Index}";
}

public sealed class NameReferenceInstructionNode : IndirectReferenceInstructionNode
{
    public readonly IdentityNode Name;

    public NameReferenceInstructionNode(
        IdentityNode opCode,
        bool isArgumentIndirection,
        IdentityNode name,
        LabelNode[] labels,
        Location? location) :
        base(opCode, isArgumentIndirection, labels, location) =>
        this.Name = name;

    public override bool Equals(Node? rhs) =>
        rhs is NameReferenceInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.IsArgumentIndirection.Equals(r.IsArgumentIndirection) &&
        this.Name.Equals(r.Name);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.IsArgumentIndirection.GetHashCode() ^
        this.Name.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out bool isArgumentIndirection,
        out IdentityNode name)
    {
        opCode = this.OpCode;
        isArgumentIndirection = this.IsArgumentIndirection;
        name = this.Name;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Name}";
}

public sealed class CallInstructionNode : InstructionNode
{
    public readonly IdentityNode Function;
    public readonly FunctionSignatureNode? Signature;

    public CallInstructionNode(
        IdentityNode opCode,
        IdentityNode function,
        FunctionSignatureNode? signature,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location)
    {
        this.Function = function;
        this.Signature = signature;
    }

    public override bool Equals(Node? rhs) =>
        rhs is CallInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Function.Equals(r.Function) &&
        (this.Signature?.Equals(r.Signature) ?? r.Signature == null);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Function.GetHashCode() ^
        this.Signature?.GetHashCode() ?? 0;

    public void Deconstruct(
        out IdentityNode opCode,
        out IdentityNode function,
        out FunctionSignatureNode? signature)
    {
        opCode = this.OpCode;
        function = this.Function;
        signature = this.Signature;
    }

    public override string ToString() =>
        this.Signature is { } signature ?
            $"{base.ToString()} {this.OpCode} {signature} {this.Function}" :
            $"{base.ToString()} {this.OpCode} {this.Function}";
}

public sealed class SignatureInstructionNode : InstructionNode
{
    public readonly FunctionSignatureNode Signature;

    public SignatureInstructionNode(
        IdentityNode opCode,
        FunctionSignatureNode signature,
        LabelNode[] labels,
        Location? location) :
        base(opCode, labels, location) =>
        this.Signature = signature;

    public override bool Equals(Node? rhs) =>
        rhs is SignatureInstructionNode r &&
        this.OpCode.Equals(r.OpCode) &&
        this.Signature.Equals(r.Signature);

    public override int GetHashCode() =>
        this.OpCode.GetHashCode() ^
        this.Signature.GetHashCode();

    public void Deconstruct(
        out IdentityNode opCode,
        out FunctionSignatureNode signature)
    {
        opCode = this.OpCode;
        signature = this.Signature;
    }

    public override string ToString() =>
        $"{base.ToString()} {this.OpCode} {this.Signature}";
}

/////////////////////////////////////////////////////////////////////

public abstract class DeclarationNode : TokenNode
{
    public readonly ScopeDescriptorNode Scope;

    protected DeclarationNode(
        ScopeDescriptorNode scope,
        Token token) :
        base(token) =>
        this.Scope = scope;
}

public abstract class NamedDeclarationNode : DeclarationNode
{
    public readonly IdentityNode Name;
    
    protected NamedDeclarationNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        Token token) :
        base(scope, token) =>
        this.Name = name;
}

/////////////////////////////////////////////////////////////////////

public abstract class FunctionDescriptorNode : NamedDeclarationNode
{
    public readonly LocalVariableNode[] LocalVariables;
    public readonly InstructionNode[] Instructions;

    protected FunctionDescriptorNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        LocalVariableNode[] localVariables,
        InstructionNode[] instructions,
        Token token) :
        base(name, scope, token)
    {
        this.LocalVariables = localVariables;
        this.Instructions = instructions;
    }

    public override bool Equals(Node? rhs) =>
        rhs is FunctionDescriptorNode r &&
        this.Scope.Equals(r.Scope) &&
        this.LocalVariables.SequenceEqual(r.LocalVariables) &&
        this.Instructions.SequenceEqual(r.Instructions);

    public override int GetHashCode() =>
        this.Scope.GetHashCode() ^
        CalculateHashCode(this.LocalVariables) ^
        CalculateHashCode(this.Instructions);
}

public sealed class FunctionNode : FunctionDescriptorNode
{
    public readonly FunctionSignatureNode Signature;

    public FunctionNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        FunctionSignatureNode signature,
        LocalVariableNode[] localVariables,
        InstructionNode[] instructions,
        Token token) :
        base(name, scope, localVariables, instructions, token) =>
        this.Signature = signature;

    public override bool Equals(Node? rhs) =>
        rhs is FunctionNode r &&
        this.Name.Equals(r.Name) &&
        this.Signature.Equals(r.Signature) &&
        base.Equals(r);

    public override int GetHashCode() =>
        this.Name.GetHashCode() ^
        this.Signature.GetHashCode() ^
        base.GetHashCode();

    public void Deconstruct(
        out IdentityNode name,
        out ScopeDescriptorNode scope,
        out FunctionSignatureNode signature,
        out LocalVariableNode[] localVariables,
        out InstructionNode[] instructions,
        out Token token)
    {
        name = this.Name;
        scope = this.Scope;
        signature = this.Signature;
        localVariables = this.LocalVariables;
        instructions = this.Instructions;
        token = this.Token;
    }

    public override string ToString() =>
        $".function {this.Scope} {this.Signature} {this.Name} LV={this.LocalVariables.Length} INSTS={this.Instructions.Length}";
}

public sealed class InitializerNode : FunctionDescriptorNode
{
    private static readonly IdentityNode name = 
        IdentityNode.Create("initializer");
    
    public InitializerNode(
        ScopeDescriptorNode scope,
        LocalVariableNode[] localVariables,
        InstructionNode[] instructions,
        Token token) :
        base(name, scope, localVariables, instructions, token)
    {
    }

    public override bool Equals(Node? rhs) =>
        rhs is InitializerNode r &&
        base.Equals(r);

    public override int GetHashCode() =>
        base.GetHashCode();

    public void Deconstruct(
        out ScopeDescriptorNode scope,
        out LocalVariableNode[] localVariables,
        out InstructionNode[] instructions,
        out Token token)
    {
        scope = this.Scope;
        localVariables = this.LocalVariables;
        instructions = this.Instructions;
        token = this.Token;
    }

    public override string ToString() =>
        $".initializer {this.Scope} LV={this.LocalVariables.Length} INSTS={this.Instructions.Length}";
}

/////////////////////////////////////////////////////////////////////

public sealed class InitializingDataNode : TokenNode
{
    public readonly byte[] Data;

    public InitializingDataNode(
        byte[] data,
        Token token) :
        base(token) =>
        this.Data = data;
    
    public override bool Equals(Node? rhs) =>
        rhs is InitializingDataNode r &&
        this.Data.SequenceEqual(r.Data);

    public override int GetHashCode() =>
        CalculateHashCode(this.Data);

    public void Deconstruct(
        out byte[] data,
        out Token token)
    {
        data = this.Data;
        token = this.Token;
    }

    public override string ToString() =>
        $"[{this.Data.Length}] {{ ... }}";
}

public abstract class VariableDeclarationNode : NamedDeclarationNode
{
    public readonly TypeNode Type;

    protected VariableDeclarationNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        TypeNode type,
        Token token) :
        base(name, scope, token) =>
        this.Type = type;
    
    public override bool Equals(Node? rhs) =>
        rhs is VariableDeclarationNode r &&
        this.Name.Equals(r.Name) &&
        this.Scope.Equals(r.Scope) &&
        this.Type.Equals(r.Type);

    public override int GetHashCode() =>
        this.Name.GetHashCode() ^
        this.Scope.GetHashCode() ^
        this.Type.GetHashCode();
}

public sealed class GlobalVariableNode : VariableDeclarationNode
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
    
    public override bool Equals(Node? rhs) =>
        rhs is GlobalVariableNode r &&
        (this.InitializingData?.Equals(r.InitializingData) ?? r.InitializingData == null) &&
        base.Equals(r);

    public override int GetHashCode() =>
        this.InitializingData?.GetHashCode() ?? 0 ^
        base.GetHashCode();

    public void Deconstruct(
        out IdentityNode name,
        out ScopeDescriptorNode scope,
        out TypeNode type,
        out InitializingDataNode? initializingDataNode,
        out Token token)
    {
        name = this.Name;
        scope = this.Scope;
        type = this.Type;
        initializingDataNode = this.InitializingData;
        token = this.Token;
    }

    public override string ToString() =>
        this.InitializingData is { } initializingData ?
            $".global {this.Scope} {this.Type} {this.Name} {initializingData}" :
            $".global {this.Scope} {this.Type} {this.Name}";
}

public sealed class GlobalConstantNode : VariableDeclarationNode
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
    
    public override bool Equals(Node? rhs) =>
        rhs is GlobalConstantNode r &&
        this.InitializingData.Equals(r.InitializingData) &&
        base.Equals(r);

    public override int GetHashCode() =>
        this.InitializingData.GetHashCode() ^
        base.GetHashCode();

    public void Deconstruct(
        out IdentityNode name,
        out ScopeDescriptorNode scope,
        out TypeNode type,
        out InitializingDataNode initializingDataNode,
        out Token token)
    {
        name = this.Name;
        scope = this.Scope;
        type = this.Type;
        initializingDataNode = this.InitializingData;
        token = this.Token;
    }

    public override string ToString() =>
        $".constant {this.Scope} {this.Type} {this.Name} {this.InitializingData}";
}

/////////////////////////////////////////////////////////////////////

public abstract class TypeDeclarationNode : NamedDeclarationNode
{
    protected TypeDeclarationNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        Token token) :
        base(name, scope, token)
    {
    }
}

/////////////////////////////////////////////////////////////////////

public sealed class EnumerationValueNode : Node
{
    public readonly IdentityNode Name;
    public readonly NumericNode? Value;

    public EnumerationValueNode(
        IdentityNode name,
        NumericNode? value)
    {
        this.Name = name;
        this.Value = value;
    }
    
    public override bool Equals(Node? rhs) =>
        rhs is EnumerationValueNode r &&
        this.Name.Equals(r.Name) &&
        (this.Value?.Equals(r.Value) ?? r.Value == null);

    public override int GetHashCode() =>
        this.Name.GetHashCode() ^
        this.Value?.GetHashCode() ?? 0;

    public void Deconstruct(
        out IdentityNode name,
        out NumericNode? value)
    {
        name = this.Name;
        value = this.Value;
    }

    public override string ToString() =>
        this.Value is { } value ?
            $"{this.Name}={value}" :
            $"{this.Name}";
}

public sealed class EnumerationNode : TypeDeclarationNode
{
    public readonly TypeNode UnderlyingType;
    public readonly EnumerationValueNode[] Values;
    
    public EnumerationNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        TypeNode underlyingType,
        EnumerationValueNode[] values,
        Token token) :
        base(name, scope, token)
    {
        this.UnderlyingType = underlyingType;
        this.Values = values;
    }
    
    public override bool Equals(Node? rhs) =>
        rhs is EnumerationNode r &&
        this.Name.Equals(r.Name) &&
        this.Scope.Equals(r.Scope) &&
        this.UnderlyingType.Equals(r.UnderlyingType) &&
        this.Values.SequenceEqual(r.Values);

    public override int GetHashCode() =>
        this.Name.GetHashCode() ^
        this.Scope.GetHashCode() ^
        this.UnderlyingType.GetHashCode() ^
        CalculateHashCode(this.Values);

    public void Deconstruct(
        out IdentityNode name,
        out ScopeDescriptorNode scope,
        out TypeNode underlyingType,
        out EnumerationValueNode[] values,
        out Token token)
    {
        name = this.Name;
        scope = this.Scope;
        underlyingType = this.UnderlyingType;
        values = this.Values;
        token = this.Token;
    }

    public override string ToString() =>
        $".enumeration {this.Scope} {this.UnderlyingType} {this.Name} VALUES={this.Values.Length}";
}

/////////////////////////////////////////////////////////////////////

public sealed class StructureFieldNode : Node
{
    public readonly ScopeDescriptorNode Scope;
    public readonly TypeNode Type;
    public readonly IdentityNode Name;
    public readonly NumericNode? Offset;

    public StructureFieldNode(
        ScopeDescriptorNode scope,
        TypeNode type,
        IdentityNode name,
        NumericNode? offset)
    {
        this.Scope = scope;
        this.Type = type;
        this.Name = name;
        this.Offset = offset;
    }
    
    public override bool Equals(Node? rhs) =>
        rhs is StructureFieldNode r &&
        this.Scope.Equals(r.Scope) &&
        this.Type.Equals(r.Type) &&
        this.Name.Equals(r.Name) &&
        (this.Offset?.Equals(r.Offset) ?? r.Offset == null);

    public override int GetHashCode() =>
        this.Scope.GetHashCode() ^
        this.Type.GetHashCode() ^
        this.Name.GetHashCode() ^
        this.Offset?.GetHashCode() ?? 0;

    public void Deconstruct(
        out ScopeDescriptorNode scope,
        out TypeNode type,
        out IdentityNode name,
        out NumericNode? offset)
    {
        scope = this.Scope;
        type = this.Type;
        name = this.Name;
        offset = this.Offset;
    }

    public override string ToString() =>
        this.Offset is { } offset ?
            $"{this.Scope} {this.Type} {this.Name} offset={offset}" :
            $"{this.Scope} {this.Type} {this.Name}";
}

public sealed class StructureNode : TypeDeclarationNode
{
    public readonly BooleanNode? IsExplicit;
    public readonly NumericNode? PackSize;
    public readonly StructureFieldNode[] Fields;
    
    public StructureNode(
        IdentityNode name,
        ScopeDescriptorNode scope,
        BooleanNode? isExplicit,
        NumericNode? packSize,
        StructureFieldNode[] fields,
        Token token) :
        base(name, scope, token)
    {
        this.IsExplicit = isExplicit;
        this.PackSize = packSize;
        this.Fields = fields;
    }
    
    public override bool Equals(Node? rhs) =>
        rhs is StructureNode r &&
        this.Name.Equals(r.Name) &&
        this.Scope.Equals(r.Scope) &&
        (this.IsExplicit?.Equals(r.IsExplicit) ?? r.IsExplicit == null) &&
        (this.PackSize?.Equals(r.PackSize) ?? r.PackSize == null) &&
        this.Fields.SequenceEqual(r.Fields);

    public override int GetHashCode() =>
        this.Name.GetHashCode() ^
        this.Scope.GetHashCode() ^
        this.IsExplicit?.GetHashCode() ?? 0 ^
        this.PackSize?.GetHashCode() ?? 0 ^
        CalculateHashCode(this.Fields);

    public void Deconstruct(
        out IdentityNode name,
        out ScopeDescriptorNode scope,
        out BooleanNode? isExplicit,
        out NumericNode? packSize,
        out StructureFieldNode[] fields,
        out Token token)
    {
        name = this.Name;
        scope = this.Scope;
        isExplicit = this.IsExplicit;
        packSize = this.PackSize;
        fields = this.Fields;
        token = this.Token;
    }

    public override string ToString() =>
        this.IsExplicit is (true, _) ?
            $".structure {this.Scope} {this.Name} explicit FIELDS={this.Fields.Length}" :
            this.PackSize is { } packSize ?
                $".structure {this.Scope} {this.Name} pack={packSize} FIELDS={this.Fields.Length}" :
                $".structure {this.Scope} {this.Name} FIELDS={this.Fields.Length}";
}
