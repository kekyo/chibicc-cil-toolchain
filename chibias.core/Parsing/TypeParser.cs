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
}

internal sealed class FunctionSignatureNode : TypeNode
{
    public readonly TypeNode ReturnType;
    public readonly TypeNode[] ParameterTypes;
    public readonly MethodCallingConvention CallingConvention;

    public FunctionSignatureNode(
        TypeNode returnType, TypeNode[] parameterTypes,
        MethodCallingConvention callingConvention)
    {
        this.ReturnType = returnType;
        this.ParameterTypes = parameterTypes;
        this.CallingConvention = callingConvention;
    }

    public override string ToString() =>
        $"{this.ReturnType}({string.Join(",", this.ParameterTypes.Select(t => t.ToString()))})";
}

internal static class TypeParser
{
    public static bool TryParse(string typeName, out TypeNode typeNode)
    {
        var nodeStack = new Stack<TypeNode>();
        var parameterNodes = new List<TypeNode>();
        var sb = new StringBuilder();
        TypeNode? currentNode = null;

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
                else if (Utilities.TryParseInt32(typeName.Substring(start, index - start), out var length) &&
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
                nodeStack.Push(currentNode);
                currentNode = null;
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
                parameterNodes.Add(currentNode);
                currentNode = null;
            }
            else if (inch == ')')
            {
                // `)`
                if (currentNode == null)
                {
                    typeNode = null!;
                    return false;
                }
                // `()`
                if (nodeStack.Count == 0)
                {
                    typeNode = null!;
                    return false;
                }

                index++;
                parameterNodes.Add(currentNode);

                var returnNode = nodeStack.Pop();
                if (parameterNodes.LastOrDefault() is TypeIdentityNode lastNode &&
                    lastNode.Identity == "...")
                {
                    currentNode = new FunctionSignatureNode(
                        returnNode,
                        parameterNodes.Take(parameterNodes.Count - 1).ToArray(),
                        MethodCallingConvention.VarArg);
                }
                else
                {
                    currentNode = new FunctionSignatureNode(
                        returnNode,
                        parameterNodes.ToArray(),
                        MethodCallingConvention.Default);
                }
                parameterNodes.Clear();
            }
            // Others (identity)
            else
            {
                Debug.Assert(currentNode == null);

                sb.Append(inch);
                index++;
                while (index < typeName.Length)
                {
                    inch = typeName[index];
                    if (inch == '*' || inch == '&' ||
                        inch == '[' || inch == ']' ||
                        inch == '(' || inch == ',' || inch == ')')
                    {
                        break;
                    }
                    sb.Append(inch);
                    index++;
                }

                currentNode = new TypeIdentityNode(sb.ToString());
                sb.Clear();
            }
        }

        if (currentNode != null && nodeStack.Count == 0 && parameterNodes.Count == 0)
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
