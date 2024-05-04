/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Parsing;
using chibild.Internal;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace chibild.Generating;

partial class CodeGenerator
{
    private void DelayLookingUpAction1(Action action)
    {
        lock (this.delayLookingUpEntries1)
        {
            this.delayLookingUpEntries1.Enqueue(action);
        }
    }

    private void DelayLookingUpAction2(Action action)
    {
        lock (this.delayLookingUpEntries2)
        {
            this.delayLookingUpEntries2.Enqueue(action);
        }
    }
    
    //////////////////////////////////////////////////////////////

    private bool ContainsPriorityTypeDeclaration(
        LookupContext context,
        TypeNode type,
        out InputFragment declaredFragment)
    {
        // Step 1: Check on current fragment (file scoped).
        if ((context.CurrentFragment?.
            ContainsTypeAndSchedule(type, out var scope) ?? false) &&
            scope == Scopes.File)
        {
            declaredFragment = context.CurrentFragment;
            return true;
        }

        // Step 2: Check on entire fragments.
        foreach (var fragment in context.InputFragments)
        {
            if (fragment.ContainsTypeAndSchedule(type, out scope) &&
                scope is Scopes.Public or Scopes.Internal)  // Internal only in another object.
            {
                declaredFragment = fragment;
                return true;
            }
        }

        declaredFragment = null!;
        return false;
    }

    private bool ContainsPriorityVariableDeclaration(
        LookupContext context,
        IdentityNode variable,
        out InputFragment declaredFragment)
    {
        // Step 1: Check on current fragment (file scoped).
        if ((context.CurrentFragment?.
            ContainsVariableAndSchedule(variable, out var scope) ?? false) &&
            scope == Scopes.File)
        {
            declaredFragment = context.CurrentFragment;
            return true;
        }

        // Step 2: Check on entire fragments.
        foreach (var fragment in context.InputFragments)
        {
            if (fragment.ContainsVariableAndSchedule(variable, out scope) &&
                scope is Scopes.Public or Scopes.Internal)  // Internal only in another object.
            {
                declaredFragment = fragment;
                return true;
            }
        }

        declaredFragment = null!;
        return false;
    }
    
    //////////////////////////////////////////////////////////////
    
    private bool InternalDelayLookingUpType(
        LookupContext context,
        TypeNode type,
        bool isFileScoped,
        Action<TypeReference> action)
    {
        // Step 1: Check on current fragment (file scoped).
        if ((context.CurrentFragment?.
            ContainsTypeAndSchedule(type, out var scope) ?? false) &&
            scope == Scopes.File)
        {
            this.DelayLookingUpAction1(() =>
            {
                if (context.CurrentFragment.TryGetType(
                    type,
                    context.FallbackModule,
                    out var tr))
                {
                    action(tr);
                }
                else
                {
                    Debug.Fail($"Lost the type declaration: {type}");
                }
            });
            return true;
        }

        // Step 2: Check on entire fragments.
        if (!isFileScoped)
        {
            InputFragment? foundFragment = null;
            foreach (var fragment in context.InputFragments)
            {
                if (fragment.ContainsTypeAndSchedule(type, out scope) &&
                    scope is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                {
                    // Found the symbol.
                    foundFragment = fragment;
                    break;
                }
            }

            if (foundFragment != null)
            {
                this.DelayLookingUpAction1(() =>
                {
                    if (foundFragment.TryGetType(
                        type,
                        context.FallbackModule,
                        out var tr))
                    {
                        action(tr);
                    }
                    else
                    {
                        Debug.Fail($"Lost the type declaration: {type}");
                    }
                });
                return true;
            }
        }

        return false;
    }

    private static void InvokeLookUpTypeActionIfCompleted(
        TypeNode type,
        TypeNode[] requiredPrioritizedTypes,
        Dictionary<string, TypeReference> resolvedTypeReferences,
        Action<TypeReference> action)
    {
        // All types to be resolved (except unresolved fixed length array types.)
        if (resolvedTypeReferences.Count >= requiredPrioritizedTypes.Length)
        {
            // Finally construct .NET type from `type`.
            var ct = TypeGenerator.ConstructCilType(
                type,
                resolvedTypeReferences);
                
            // Call action.
            action(ct);
        }
    }
        
    private void DelayLookingUpType(
        LookupContext context,
        TypeNode type,
        bool isFileScoped,
        Action<TypeReference> action,
        Action? didNotResolve = null)
    {
        // This result preserves the order of the required types.
        // Therefore, if the types are resolved in the order of this list,
        // we should be able to construct each .NET type.
        var requiredPrioritizedTypes =
            TypeGenerator.FilterRequiredPrioritizedCilBasisTypes(type);
        
        // If contains `FixedLengthArrayTypeNode`,
        // then we need to add the necessary types here to construct the fixed length array type.
        // When finally construct the .NET type with `TryConstructCilType()`,
        // it is assumed that these .NET types are also prepared in `resolvedTypeReferences`.
        if (requiredPrioritizedTypes.Any(rt => rt is FixedLengthArrayTypeNode))
        {
            requiredPrioritizedTypes =
                TypeGenerator.FixedLengthArrayTypeConstructRequirementTypes.
                Concat(requiredPrioritizedTypes).
                Distinct().
                ToArray();
        }

        var resolvedTypeReferences = new Dictionary<string, TypeReference>();
        var lackTypes = false;
        
        foreach (var requiredType in requiredPrioritizedTypes)
        {
            if (!this.InternalDelayLookingUpType(
                context,
                requiredType,
                isFileScoped,
                tr =>
                {
                    // Append resolved .NET type.
                    resolvedTypeReferences.Add(
                        requiredType.CilTypeName,
                        // By importing the type in advance, it is possible to use the type as it is after synthesis.
                        context.SafeImport(tr));

                    // Invoke action if all types resolved.
                    InvokeLookUpTypeActionIfCompleted(
                        type,
                        requiredPrioritizedTypes,
                        resolvedTypeReferences,
                        action);
                }))
            {
                // Could not find fixed length array type in other loaded assembly fragments.
                if (requiredType is FixedLengthArrayTypeNode flat)
                {
                    this.DelayLookingUpAction1(() =>
                    {
                        // At this point, the `resolvedTypeReferences` should contain
                        // all the `TypeReferences` needed to construct a fixed length array type.
                        if (TypeGenerator.TryGetFixedLengthArrayType(
                            context.FallbackModule,
                            flat,
                            resolvedTypeReferences,
                            out var flatr,
                            this.OutputError))
                        {
                            // Append got .NET type.
                            resolvedTypeReferences.Add(
                                flat.CilTypeName,
                                context.SafeImport(flatr));

                            // Invoke action if all types resolved.
                            InvokeLookUpTypeActionIfCompleted(
                                type,
                                requiredPrioritizedTypes,
                                resolvedTypeReferences,
                                action);
                        }
                    });
                }
                // Could not find.
                else
                {
                    lackTypes = true;
                }
            }
        }

        if (lackTypes)
        {
            if (didNotResolve != null)
            {
                this.DelayLookingUpAction1(didNotResolve);
            }
            else
            {
                this.OutputError(
                    type.Token,
                    $"Could not find a type: {type}");
            }
        }
    }

    private void DelayLookingUpType(
        LookupContext context,
        IdentityNode type,
        bool isFileScoped,
        Action<TypeReference> action,
        Action? didNotResolve = null) =>
        this.DelayLookingUpType(
            context,
            TypeParser.TryParse(type.Token, out var t) ?
                t : throw new InvalidOperationException(),
            isFileScoped,
            action,
            didNotResolve);

    //////////////////////////////////////////////////////////////

    private void DelayLookingUpField(
        LookupContext context,
        IdentityNode field,
        bool isFileScoped,
        Action<FieldReference> action,
        Action? didNotResolve = null)
    {
        // Step 1: Check on current fragment (file scoped).
        if ((context.CurrentFragment?.
            ContainsVariableAndSchedule(field, out var scope) ?? false) &&
            scope == Scopes.File)
        {
            this.DelayLookingUpAction2(() =>
            {
                if (context.CurrentFragment.TryGetField(
                    field,
                    context.FallbackModule,
                    out var tr))
                {
                    action(tr);
                }
                else
                {
                    Debug.Fail($"Lost the field declaration: {field}");
                }
            });
            return;
        }

        // Step 2: Check on entire fragments.
        if (!isFileScoped)
        {
            InputFragment? foundFragment = null;
            foreach (var fragment in context.InputFragments)
            {
                if (fragment.ContainsVariableAndSchedule(field, out scope) &&
                    scope is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                {
                    // Found the symbol.
                    foundFragment = fragment;
                    break;
                }
            }

            if (foundFragment != null)
            {
                this.DelayLookingUpAction2(() =>
                {
                    if (foundFragment.TryGetField(
                        field,
                        context.FallbackModule,
                        out var tr))
                    {
                        action(tr);
                    }
                    else
                    {
                        Debug.Fail($"Lost the field declaration: {field}");
                    }
                });
                return;
            }
        }

        if (didNotResolve != null)
        {
            this.DelayLookingUpAction2(didNotResolve);
        }
        else
        {
            this.OutputError(
                field.Token,
                $"Could not find a variable: {field}");
        }
    }

    //////////////////////////////////////////////////////////////

    private void DelayLookingUpMethod(
        LookupContext context,
        IdentityNode function,
        FunctionSignatureNode? signature,
        bool isFileScoped,
        Action<MethodReference, TypeReference[]> action,
        Action? didNotResolve = null)
    {
        var resolvedParameterTypes = new List<TypeReference>();

        if (signature != null)
        {
            foreach (var parameter in signature.Parameters)
            {
                this.DelayLookingUpType(
                    context,
                    parameter.ParameterType,
                    false,
                    resolvedParameterTypes.Add);
            }
        }
        
        // Step 1: Check on current fragment (file scoped).
        if ((context.CurrentFragment?.
            ContainsFunctionAndSchedule(function, signature, out var scope) ?? false) &&
            scope == Scopes.File)
        {
            this.DelayLookingUpAction2(() =>
            {
                if (context.CurrentFragment.TryGetMethod(
                    function,
                    signature,
                    context.FallbackModule,
                    out var method))
                {
                    Debug.Assert(resolvedParameterTypes.Count == (signature?.Parameters.Length ?? 0));
                    
                    action(method, resolvedParameterTypes.ToArray());
                }
                else
                {
                    Debug.Fail($"Lost the method declaration: {function}({signature})");
                }
            });
            return;
        }

        // Step 2: Check on entire fragments.
        if (!isFileScoped)
        {
            InputFragment? foundFragment = null;
            foreach (var fragment in context.InputFragments)
            {
                if (fragment.ContainsFunctionAndSchedule(function, signature, out scope) &&
                    scope is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                {
                    // Found the symbol.
                    foundFragment = fragment;
                    break;
                }
            }

            if (foundFragment != null)
            {
                this.DelayLookingUpAction2(() =>
                {
                    if (foundFragment.TryGetMethod(
                        function,
                        signature,
                        context.FallbackModule,
                        out var method))
                    {
                        Debug.Assert(resolvedParameterTypes.Count == (signature?.Parameters.Length ?? 0));
                    
                        action(method, resolvedParameterTypes.ToArray());
                    }
                    else
                    {
                        Debug.Fail($"Lost the method declaration: {function}({signature})");
                    }
                });
                return;
            }
        }

        if (didNotResolve != null)
        {
            this.DelayLookingUpAction2(didNotResolve);
        }
        else
        {
            this.OutputError(
                function.Token,
                $"Could not find a function: {function}");
        }
    }

    //////////////////////////////////////////////////////////////

    private enum FoundMembers
    {
        None,
        Type,
        Variable,
        Function,
    }

    private void DelayLookingUpMember(
        LookupContext context,
        IdentityNode member,
        FunctionSignatureNode? functionSignature,
        bool isFileScoped,
        Action<MemberReference> action,
        Action? didNotResolve = null)
    {
        // Step 1: Check on current fragment (file scoped).
        TypeNode? type = null;
        if (functionSignature == null)
        {
            type = TypeParser.TryParse(member.Token, out var t) ? t : null;
            if (type != null &&
                (context.CurrentFragment?.
                 ContainsTypeAndSchedule(type, out var scope) ?? false) &&
                scope == Scopes.File)
            {
                this.DelayLookingUpAction1(() =>
                {
                    if (context.CurrentFragment.TryGetType(
                        type,
                        context.FallbackModule,
                        out var tr))
                    {
                        action(tr);
                    }
                    else
                    {
                        Debug.Fail($"Lost the type declaration: {type}");
                    }
                });
                return;
            }

            if ((context.CurrentFragment?.
                ContainsVariableAndSchedule(member, out scope) ?? false) &&
                scope == Scopes.File)
            {
                this.DelayLookingUpAction2(() =>
                {
                    if (context.CurrentFragment.TryGetField(
                        member,
                        context.FallbackModule,
                        out var tr))
                    {
                        action(tr);
                    }
                    else
                    {
                        Debug.Fail($"Lost the field declaration: {member}");
                    }
                });
                return;
            }
        }

        if ((context.CurrentFragment?.ContainsFunctionAndSchedule(
            member, functionSignature, out var scope2) ?? false) &&
            scope2 == Scopes.File)
        {
            this.DelayLookingUpAction2(() =>
            {
                if (context.CurrentFragment.TryGetMethod(
                    member,
                    functionSignature,
                    context.FallbackModule,
                    out var mr))
                {
                    action(mr);
                }
                else
                {
                    Debug.Fail($"Lost the method declaration: {mr}({functionSignature})");
                }
            });
            return;
        }

        // Step 2: Check on entire fragments.
        if (!isFileScoped)
        {
            InputFragment? foundFragment = null;
            var found = FoundMembers.None;

            if (functionSignature == null)
            {
                foreach (var fragment in context.InputFragments)
                {
                    if (type != null &&
                        fragment.ContainsTypeAndSchedule(type, out var scope3) &&
                        scope3 is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                    {
                        // Found the symbol.
                        foundFragment = fragment;
                        found = FoundMembers.Type;
                        break;
                    }
                    if (fragment.ContainsVariableAndSchedule(member, out scope3) &&
                        scope3 is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                    {
                        // Found the symbol.
                        foundFragment = fragment;
                        found = FoundMembers.Variable;
                        break;
                    }
                    if (fragment.ContainsFunctionAndSchedule(member, null, out scope3) &&
                        scope3 is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                    {
                        // Found the symbol.
                        foundFragment = fragment;
                        found = FoundMembers.Function;
                        break;
                    }
                }
            }
            else
            {
                foreach (var fragment in context.InputFragments)
                {
                    if (fragment.ContainsFunctionAndSchedule(
                        member, functionSignature, out var scope4) &&
                        scope4 is Scopes.Public or Scopes.Internal)  // Internal only in another object.
                    {
                        // Found the symbol.
                        foundFragment = fragment;
                        found = FoundMembers.Function;
                        break;
                    }
                }
            }

            if (foundFragment != null)
            {
                switch (found)
                {
                    case FoundMembers.Type:
                        this.DelayLookingUpAction1(() =>
                        {
                            Debug.Assert(type != null);
                            Debug.Assert(functionSignature == null);
                            if (foundFragment.TryGetType(
                                type!,
                                context.FallbackModule,
                                out var tr))
                            {
                                action(tr);
                                return;
                            }
                            Debug.Fail($"Lost the member declaration: {member}{(functionSignature is { } s ? $"({s})" : "")}");
                        });
                        break;
                    case FoundMembers.Variable:
                    case FoundMembers.Function:
                        this.DelayLookingUpAction2(() =>
                        {
                            switch (found)
                            {
                                case FoundMembers.Variable:
                                    Debug.Assert(functionSignature == null);
                                    if (foundFragment.TryGetField(
                                        member,
                                        context.FallbackModule,
                                        out var fr))
                                    {
                                        action(fr);
                                        return;
                                    }
                                    break;
                                case FoundMembers.Function:
                                    if (foundFragment.TryGetMethod(
                                        member,
                                        functionSignature,
                                        context.FallbackModule,
                                        out var mr))
                                    {
                                        action(mr);
                                        return;
                                    }
                                    break;
                            }
                            Debug.Fail($"Lost the member declaration: {member}{(functionSignature is { } s ? $"({s})" : "")}");
                        });
                        break;
                    default:
                        Debug.Fail("");
                        return;
                }
                return;
            }
        }

        if (didNotResolve != null)
        {
            this.DelayLookingUpAction2(didNotResolve);
        }
        else
        {
            this.OutputError(
                member.Token,
                $"Could not find a member: {member}");
        }
    }
}
