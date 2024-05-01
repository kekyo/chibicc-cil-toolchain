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
        public readonly List<FunctionParameterNode> Parameters;

        public FunctionDescriptor(OuterNode node, List<FunctionParameterNode> parameters)
        {
            this.Node = node;
            this.Parameters = parameters;
        }

        public void Deconstruct(out OuterNode node, out List<FunctionParameterNode> parameters)
        {
            node = this.Node;
            parameters = this.Parameters;
        }
    }

    ///////////////////////////////////////////////////////////////////////////

    public static bool TryParse(Token token, out TypeNode typeNode)
    {
        var nodeStack = new Stack<FunctionDescriptor>();
        var parameters = new List<FunctionParameterNode>();
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
                    currentNode = new ArrayTypeNode(currentNode, token);
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
                    currentNode = new FixedLengthArrayTypeNode(currentNode, length, token);
                }
                // `foo[*]`
                else if (typeName.Substring(start, index - start) == "*")
                {
                    index++;
                    currentNode = new FixedLengthArrayTypeNode(currentNode, -1, token);
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
                if (parameters.LastOrDefault() is (TypeIdentityNode("...", _), _, _))
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

    ///////////////////////////////////////////////////////////////////////////

    internal static string GetCilTypeName(TypeNode type)
    {
        // This method produces output similar to `TypeNode.ToString()`.
        // The difference is that when a fixed length array type is included,
        // the name of the corresponding .NET type.

        static string InnerFixedLengthArrayElementTypeName(TypeNode type)
        {
            switch (type)
            {
                // .NET array type.
                case ArrayTypeNode(var elementType, _):
                    return $"{InnerFixedLengthArrayElementTypeName(elementType)}_arr";

                // Fixed length array type.
                case FixedLengthArrayTypeNode(var elementType, var length, _):
                    var etn1 = InnerFixedLengthArrayElementTypeName(elementType);
                    return length >= 0 ?
                        $"{etn1}_len{length}" :  // Fixed length array type.
                        throw new ArgumentException();

                // Nested reference type.
                case DerivedTypeNode(DerivedTypes.Reference, var elementType, _):
                    return $"{InnerFixedLengthArrayElementTypeName(elementType)}_ref";

                // Nested pointer type.
                case DerivedTypeNode(DerivedTypes.Pointer, var elementType, _):
                    return $"{InnerFixedLengthArrayElementTypeName(elementType)}_ptr";

                // Function signature.
                case FunctionSignatureNode(var returnType, var parameters, _, _):
                    var rtn = InnerFixedLengthArrayElementTypeName(returnType);
                    var ptns = parameters.
                        Select(p => InnerFixedLengthArrayElementTypeName(p.ParameterType)).
                        ToArray();
                    // TODO: very weak mangling for any parameter types.
                    return $"func_{rtn}_{string.Join("_", ptns)}";

                default:
                    return type.TypeIdentity;
            }
        }

        static string InnerTypeName(TypeNode type)
        {
            switch (type)
            {
                // .NET array type.
                case ArrayTypeNode(var elementType, _):
                    return $"{InnerTypeName(elementType)}[]";

                // Fixed length array type.
                case FixedLengthArrayTypeNode(var elementType, var length, _):
                    var etn1 = InnerFixedLengthArrayElementTypeName(elementType);
                    return length >= 0 ?
                        $"{etn1}_len{length}" :  // Fixed length array type.
                        $"{etn1}_flex";          // Flex array type.

                // Nested reference type.
                case DerivedTypeNode(DerivedTypes.Reference, var elementType, _):
                    return $"{InnerTypeName(elementType)}&";

                // Nested pointer type.
                case DerivedTypeNode(DerivedTypes.Pointer, var elementType, _):
                    return $"{InnerTypeName(elementType)}*";

                // Function signature.
                case FunctionSignatureNode(var returnType, var parameters, _, _):
                    var rtn = InnerTypeName(returnType);
                    var ptns = parameters.Select(p => InnerTypeName(p.ParameterType));
                    return $"{rtn}({string.Join(",", ptns)})";

                default:
                    return type.TypeIdentity;
            }
        }

        return InnerTypeName(type);
    }

    public static string GetCilTypeName(Type type)
    {
        static string InnerTypeName(Type type)
        {
            switch (type)
            {
                // .NET array type.
                case { IsArray: true }:
                    return $"{InnerTypeName(type.GetElementType()!)}[]";

                // Nested reference type.
                case { IsByRef: true }:
                    return $"{InnerTypeName(type.GetElementType()!)}&";

                // Nested pointer type.
                case { IsPointer: true }:
                    return $"{InnerTypeName(type.GetElementType()!)}*";

                default:
                    return type.Namespace == "C.type" ?
                        type.Name :
                        type.FullName!;
            }
        }
        return InnerTypeName(type);
    }
}
