/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using chibicc.toolchain.Tokenizing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace chibicc.toolchain.Parsing;

[DebuggerDisplay("{TypeIdentity}")]
public abstract class TypeNode : TokenNode
{
    protected TypeNode(Token token) :
        base(token)
    {
    }

    public string TypeIdentity =>
        this.ToString()!;

    public string CilTypeName =>
        TypeParser.GetCilTypeName(this);
}

public sealed class TypeIdentityNode : TypeNode
{
    public readonly string Identity;

    public TypeIdentityNode(
        string identity,
        Token token) :
        base(token) =>
        this.Identity = identity;
        
    public override bool Equals(Node? rhs) =>
        rhs is TypeIdentityNode r &&
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
}

public enum DerivedTypes
{
    Pointer,
    Reference,
}

public sealed class DerivedTypeNode : TypeNode
{
    public readonly DerivedTypes Type;
    public readonly TypeNode ElementType;

    public DerivedTypeNode(
        DerivedTypes type, TypeNode elementType, Token token) :
        base(token)
    {
        this.Type = type;
        this.ElementType = elementType;
    }
        
    public override bool Equals(Node? rhs) =>
        rhs is DerivedTypeNode r &&
        this.Type.Equals(r.Type) &&
        this.ElementType.Equals(r.ElementType);

    public override int GetHashCode() =>
        this.Type.GetHashCode() ^
        this.ElementType.GetHashCode();

    public void Deconstruct(
        out DerivedTypes type,
        out TypeNode elementType,
        out Token token)
    {
        type = this.Type;
        elementType = this.ElementType;
        token = this.Token;
    }

    public override string ToString() =>
        this.Type switch
        {
            DerivedTypes.Reference => $"{this.ElementType}&",
            DerivedTypes.Pointer => $"{this.ElementType}*",
            _ => $"{this.ElementType}?{{{this.Type}}}",
        };
}

public sealed class ArrayTypeNode : TypeNode
{
    public readonly TypeNode ElementType;

    public ArrayTypeNode(
        TypeNode elementType,
        Token token) :
        base(token) =>
        this.ElementType = elementType;

    public override bool Equals(Node? rhs) =>
        rhs is ArrayTypeNode r &&
        this.ElementType.Equals(r.ElementType);

    public override int GetHashCode() =>
        this.ElementType.GetHashCode();

    public void Deconstruct(
        out TypeNode elementType,
        out Token token)
    {
        elementType = this.ElementType;
        token = this.Token;
    }

    public override string ToString() =>
        $"{this.ElementType}[]";
}

public sealed class FixedLengthArrayTypeNode : TypeNode
{
    public readonly TypeNode ElementType;
    public readonly int Length;

    public FixedLengthArrayTypeNode(
        TypeNode elementType, int length, Token token) :
        base(token)
    {
        this.ElementType = elementType;
        this.Length = length;
    }
        
    public override bool Equals(Node? rhs) =>
        rhs is FixedLengthArrayTypeNode r &&
        this.ElementType.Equals(r.ElementType) &&
        (this.Length.Equals(r.Length));

    public override int GetHashCode() =>
        this.ElementType.GetHashCode() ^
        this.Length.GetHashCode();

    public void Deconstruct(
        out TypeNode elementType,
        out int length,
        out Token token)
    {
        elementType = this.ElementType;
        length = this.Length;
        token = this.Token;
    }

    public override string ToString() =>
        this.Length >= 0 ?
            $"{this.ElementType}[{this.Length}]" :
            $"{this.ElementType}[*]";
}

public sealed class FunctionParameterNode : TokenNode
{
    public readonly TypeNode ParameterType;
    public readonly string? ParameterName;

    public FunctionParameterNode(
        TypeNode parameterType,
        string? parameterName,
        Token token) :
        base(token)
    {
        this.ParameterType = parameterType;
        this.ParameterName = parameterName;
    }
        
    public override bool Equals(Node? rhs) =>
        rhs is FunctionParameterNode r &&
        this.ParameterType.Equals(r.ParameterType);

    public override int GetHashCode() =>
        this.ParameterType.GetHashCode();

    public void Deconstruct(
        out TypeNode parameterType,
        out string? parameterName,
        out Token token)
    {
        parameterType = this.ParameterType;
        parameterName = this.ParameterName;
        token = this.Token;
    }

    public override string ToString() =>
        this.ParameterName is { } parameterName ?
            $"{parameterName}:{this.ParameterType}" :
            this.ParameterType.ToString()!;
}

public enum MethodCallingConvention
{
    Default,
    VarArg,
}

// Be careful, `FunctionSignatureNode` is not a type.
public sealed class FunctionSignatureNode : TypeNode
{
    public readonly TypeNode ReturnType;
    public readonly FunctionParameterNode[] Parameters;
    public readonly MethodCallingConvention CallingConvention;

    public FunctionSignatureNode(
        TypeNode returnType,
        FunctionParameterNode[] parameters,
        MethodCallingConvention callingConvention,
        Token token) :
        base(token)
    {
        this.ReturnType = returnType;
        this.Parameters = parameters;
        this.CallingConvention = callingConvention;
    }
        
    public override bool Equals(Node? rhs) =>
        rhs is FunctionSignatureNode r &&
        this.ReturnType.Equals(r.ReturnType) &&
        (this.CallingConvention, r.CallingConvention) switch
        {
            (MethodCallingConvention.VarArg, MethodCallingConvention.VarArg) =>
                this.Parameters.Take(Math.Min(this.Parameters.Length, r.Parameters.Length)).
                    SequenceEqual(r.Parameters.Take(Math.Min(this.Parameters.Length, r.Parameters.Length))),
            (MethodCallingConvention.VarArg, _) when
                this.Parameters.Length <= r.Parameters.Length =>
                this.Parameters.SequenceEqual(r.Parameters.Take(this.Parameters.Length)),
            (_, MethodCallingConvention.VarArg) when
                r.Parameters.Length <= this.Parameters.Length =>
                r.Parameters.SequenceEqual(this.Parameters.Take(r.Parameters.Length)),
            _ => this.Parameters.SequenceEqual(r.Parameters),
        };

    public override int GetHashCode() =>
        0;  // Ignored. Can not use this class in the hashed key.

    public void Deconstruct(
        out TypeNode returnType,
        out FunctionParameterNode[] parameters,
        out MethodCallingConvention callingConvention,
        out Token token)
    {
        returnType = this.ReturnType;
        parameters = this.Parameters;
        callingConvention = this.CallingConvention;
        token = this.Token;
    }

    public override string ToString() =>
        $"{this.ReturnType}({string.Join(",", (object[])this.Parameters)})";
}
