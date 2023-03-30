/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using Mono.Cecil;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace chibias.Parsing;

internal abstract class TypeNode
{
}

internal sealed class TypeIdentityNode : TypeNode
{
    public readonly string Identity;

    public TypeIdentityNode(
        string identity) =>
        this.Identity = identity;

    public override string ToString() =>
        this.Identity;

    public void Deconstruct(
        out string identity) =>
        identity = this.Identity;
}

internal enum DerivedTypes
{
    Pointer,
    Reference,
}

internal sealed class DerivedTypeNode : TypeNode
{
    public readonly DerivedTypes Type;
    public readonly TypeNode ElementType;

    public DerivedTypeNode(
        DerivedTypes type, TypeNode elementType)
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

internal sealed class ArrayTypeNode : TypeNode
{
    public readonly TypeNode ElementType;
    public readonly int? Length;

    public ArrayTypeNode(
        TypeNode elementType, int? length)
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

internal sealed class FunctionParameter
{
    public readonly TypeNode ParameterType;
    public readonly string? ParameterName;

    public FunctionParameter(
        TypeNode parameterType, string? parameterName)
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

internal sealed class FunctionSignatureNode : TypeNode
{
    public readonly TypeNode ReturnType;
    public readonly FunctionParameter[] Parameters;
    public readonly MethodCallingConvention CallingConvention;

    public FunctionSignatureNode(
        TypeNode returnType, FunctionParameter[] parameters,
        MethodCallingConvention callingConvention)
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

internal static class TypeParser
{
    private readonly struct OuterNode
    {
        public readonly TypeNode Node;
        public readonly string? Name;

        public OuterNode(TypeNode node, string? name)
        {
            this.Node = node;
            this.Name = name;
        }

        public void Deconstruct(out TypeNode node, out string? name)
        {
            node = this.Node;
            name = this.Name;
        }
    }

    public static bool TryParse(string typeName, out TypeNode typeNode)
    {
        var nodeStack = new Stack<OuterNode>();
        var parameters = new List<FunctionParameter>();
        var sb = new StringBuilder();
        TypeNode? currentNode = null;
        string? currentName = null;

        var index = 0;
        while (index < typeName.Length)
        {
            var inch = typeName[index];

            // Pointer `foo*`
            if (inch == '*')
            {
                // `*`
                if (currentNode == null)
                {
                    typeNode = null!;
                    return false;
                }

                index++;
                currentNode = new DerivedTypeNode(DerivedTypes.Pointer, currentNode);
            }
            // Reference `foo&`
            else if (inch == '&')
            {
                // `&`
                if (currentNode == null)
                {
                    typeNode = null!;
                    return false;
                }

                index++;
                currentNode = new DerivedTypeNode(DerivedTypes.Reference, currentNode);
            }
            // Array `foo[]` `foo[123]`
            else if (inch == '[')
            {
                // `[`
                if (currentNode == null)
                {
                    typeNode = null!;
                    return false;
                }

                index++;
                var start = index;
                while (index < typeName.Length)
                {
                    inch = typeName[index];
                    if (inch == ']')
                    {
                        break;
                    }
                    index++;
                }

                // `foo[]`
                if (start == index)
                {
                    index++;
                    currentNode = new ArrayTypeNode(currentNode, null);
                }
                // `foo[123]`
                else if (Utilities.TryParseInt32(
                    typeName.Substring(start, index - start),
                    out var length) &&
                    length >= 0)
                {
                    index++;
                    currentNode = new ArrayTypeNode(currentNode, length);
                }
                // `foo[*]`
                else if (typeName.Substring(start, index - start) == "*")
                {
                    index++;
                    currentNode = new ArrayTypeNode(currentNode, -1);
                }
                // `foo[-1]` `foo[abc]`
                else
                {
                    typeNode = null!;
                    return false;
                }
            }
            // Function signature `string(int32,int8)`
            else if (inch == '(')
            {
                // `(`
                if (currentNode == null)
                {
                    typeNode = null!;
                    return false;
                }

                index++;
                nodeStack.Push(new(currentNode, currentName));
                currentNode = null;
                currentName = null;
            }
            else if (inch == ',')
            {
                // `,`
                if (currentNode == null)
                {
                    typeNode = null!;
                    return false;
                }

                index++;
                parameters.Add(new(currentNode, currentName));
                currentNode = null;
                currentName = null;
            }
            else if (inch == ')')
            {
                // `()`
                if (nodeStack.Count == 0)
                {
                    typeNode = null!;
                    return false;
                }

                index++;

                if (currentNode != null)
                {
                    parameters.Add(new(currentNode, currentName));
                }

                var (returnNode, name) = nodeStack.Pop();
                if (parameters.LastOrDefault() is FunctionParameter(TypeIdentityNode("..."), _))
                {
                    currentNode = new FunctionSignatureNode(
                        returnNode,
                        parameters.Take(parameters.Count - 1).ToArray(),
                        MethodCallingConvention.VarArg);
                }
                else
                {
                    currentNode = new FunctionSignatureNode(
                        returnNode,
                        parameters.ToArray(),
                        MethodCallingConvention.Default);
                }
                currentName = name;
                parameters.Clear();
            }
            else if (inch == ':')
            {
                if (string.IsNullOrWhiteSpace(currentName) || nodeStack.Count == 0)
                {
                    typeNode = null!;
                    return false;
                }
                index++;
            }
            // Others (identity)
            else
            {
                if (currentNode != null)
                {
                    typeNode = null!;
                    return false;
                }

                sb.Append(inch);
                index++;
                while (index < typeName.Length)
                {
                    inch = typeName[index];
                    if (inch == '*' || inch == '&' ||
                        inch == '[' || inch == ']' ||
                        inch == '(' || inch == ':' || inch == ',' || inch == ')')
                    {
                        break;
                    }
                    sb.Append(inch);
                    index++;
                }

                if (inch == ':')
                {
                    if (!string.IsNullOrWhiteSpace(currentName))
                    {
                        typeNode = null!;
                        return false;
                    }
                    currentName = sb.ToString();
                }
                else
                {
                    currentNode = new TypeIdentityNode(sb.ToString());
                }
                sb.Clear();
            }
        }

        if (currentNode != null && string.IsNullOrWhiteSpace(currentName) &&
            nodeStack.Count == 0 && parameters.Count == 0)
        {
            typeNode = currentNode;
            return true;
        }
        else
        {
            typeNode = null!;
            return false;
        }
    }
}
