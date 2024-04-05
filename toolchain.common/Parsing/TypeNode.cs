/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System.Linq;

namespace chibicc.toolchain.Parsing;

public abstract class TypeNode : Node
{
    protected TypeNode(Token token) :
        base(token)
    {
    }
}

public sealed class TypeIdentityNode : TypeNode
{
    public readonly string Identity;

    public TypeIdentityNode(
        string identity,
        Token token) :
        base(token) =>
        this.Identity = identity;

    public override string ToString() =>
        this.Identity;

    public void Deconstruct(
        out string identity) =>
        identity = this.Identity;
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

    public override string ToString() =>
        this.Type switch
        {
            DerivedTypes.Pointer => $"{this.ElementType}*",
            DerivedTypes.Reference => $"{this.ElementType}&",
            _ => $"{this.ElementType}?",
        };

    public void Deconstruct(
        out DerivedTypes type, out TypeNode elementType)
    {
        type = this.Type;
        elementType = this.ElementType;
    }
}

public sealed class ArrayTypeNode : TypeNode
{
    public readonly TypeNode ElementType;
    public readonly int? Length;

    public ArrayTypeNode(
        TypeNode elementType, int? length, Token token) :
        base(token)
    {
        this.ElementType = elementType;
        this.Length = length;
    }

    public override string ToString() =>
        this.Length switch
        {
            null => $"{this.ElementType}[]",
            { } length => $"{this.ElementType}[{length}]",
        };

    public void Deconstruct(
        out TypeNode elementType, out int? length)
    {
        elementType = this.ElementType;
        length = this.Length;
    }
}

public sealed class FunctionParameter : Node
{
    public readonly TypeNode ParameterType;
    public readonly string? ParameterName;

    public FunctionParameter(
        TypeNode parameterType, string? parameterName, Token token) :
        base(token)
    {
        this.ParameterType = parameterType;
        this.ParameterName = parameterName;
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(this.ParameterName) ?
            this.ParameterType.ToString()! :
            $"{this.ParameterName!}:{this.ParameterType}";

    public void Deconstruct(
        out TypeNode parameterType, out string? parameterName)
    {
        parameterType = this.ParameterType;
        parameterName = this.ParameterName;
    }
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
    public readonly FunctionParameter[] Parameters;
    public readonly MethodCallingConvention CallingConvention;

    public FunctionSignatureNode(
        TypeNode returnType, FunctionParameter[] parameters,
        MethodCallingConvention callingConvention,
        Token token) :
        base(token)
    {
        this.ReturnType = returnType;
        this.Parameters = parameters;
        this.CallingConvention = callingConvention;
    }

    public override string ToString() =>
        $"{this.ReturnType}({string.Join(",", this.Parameters.Select(t => t.ToString()))})";

    public void Deconstruct(
        out TypeNode returnType,
        out FunctionParameter[] parameters,
        out MethodCallingConvention callingConvention)
    {
        returnType = this.ReturnType;
        parameters = this.Parameters;
        callingConvention = this.CallingConvention;
    }
}
