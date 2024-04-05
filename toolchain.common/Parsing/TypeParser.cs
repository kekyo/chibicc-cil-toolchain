/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Tokenizing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace chibicc.toolchain.Parsing;

public static class TypeParser
{
    private static readonly Dictionary<string, string> aliasTypeNames = new()
    {
        { "void", "System.Void" },
        { "uint8", "System.Byte" },
        { "int8", "System.SByte" },
        { "int16", "System.Int16" },
        { "uint16", "System.UInt16" },
        { "int32", "System.Int32" },
        { "uint32", "System.UInt32" },
        { "int64", "System.Int64" },
        { "uint64", "System.UInt64" },
        { "float32", "System.Single" },
        { "float64", "System.Double" },
        { "nint", "System.IntPtr" },
        { "nuint", "System.UIntPtr" },
        { "bool", "System.Boolean" },
        { "char", "System.Char" },
        { "object", "System.Object" },
        { "string", "System.String" },
        { "typedref", "System.TypedReference" },
        { "byte", "System.Byte" },
        { "sbyte", "System.SByte" },
        { "short", "System.Int16" },
        { "ushort", "System.UInt16" },
        { "int", "System.Int32" },
        { "uint", "System.UInt32" },
        { "long", "System.Int64" },
        { "ulong", "System.UInt64" },
        { "single", "System.Single" },
        { "float", "System.Single" },
        { "double", "System.Double" },
        { "char16", "System.Char" },
        { "intptr", "System.IntPtr" },
        { "uintptr", "System.UIntPtr" },
    };

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

    private readonly struct FunctionDescriptor
    {
        public readonly OuterNode Node;
        public readonly List<FunctionParameter> Parameters;

        public FunctionDescriptor(OuterNode node, List<FunctionParameter> parameters)
        {
            this.Node = node;
            this.Parameters = parameters;
        }

        public void Deconstruct(out OuterNode node, out List<FunctionParameter> parameters)
        {
            node = this.Node;
            parameters = this.Parameters;
        }
    }

    ///////////////////////////////////////////////////////////////////////////

    public static bool TryParse(Token token, out TypeNode typeNode)
    {
        var nodeStack = new Stack<FunctionDescriptor>();
        var parameters = new List<FunctionParameter>();
        var sb = new StringBuilder();
        TypeNode? currentNode = null;
        string? currentName = null;

        var typeName = token.Text;
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
                currentNode = new DerivedTypeNode(DerivedTypes.Pointer, currentNode, token);
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
                currentNode = new DerivedTypeNode(DerivedTypes.Reference, currentNode, token);
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
                    currentNode = new ArrayTypeNode(currentNode, null, token);
                }
                // `foo[123]`
                else if (int.TryParse(
                    typeName.Substring(start, index - start),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var length) &&
                    length >= 0)
                {
                    index++;
                    currentNode = new ArrayTypeNode(currentNode, length, token);
                }
                // `foo[*]`
                else if (typeName.Substring(start, index - start) == "*")
                {
                    index++;
                    currentNode = new ArrayTypeNode(currentNode, -1, token);
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
                nodeStack.Push(new(new(currentNode, currentName), parameters));
                currentNode = null;
                currentName = null;
                parameters = new();
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
                parameters.Add(new(currentNode, currentName, token));
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
                    parameters.Add(new(currentNode, currentName, token));
                }

                var ((returnNode, name), lastParameters) = nodeStack.Pop();
                if (parameters.LastOrDefault() is (TypeIdentityNode("..."), _))
                {
                    currentNode = new FunctionSignatureNode(
                        returnNode,
                        parameters.Take(parameters.Count - 1).ToArray(),
                        MethodCallingConvention.VarArg,
                        token);
                }
                else
                {
                    currentNode = new FunctionSignatureNode(
                        returnNode,
                        parameters.ToArray(),
                        MethodCallingConvention.Default,
                        token);
                }
                currentName = name;
                parameters = lastParameters;
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
                    currentNode = new TypeIdentityNode(
                        aliasTypeNames.TryGetValue(sb.ToString(), out var normalizedName) ?
                            normalizedName : sb.ToString(),
                        token);
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

    public static T UnsafeParse<T>(Token token)
        where T : TypeNode =>
        TryParse(token, out var typeNode) && typeNode is T node ?
            node : throw new ArgumentException();
}
