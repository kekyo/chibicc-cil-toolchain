/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Parsing;
using chibicc.toolchain.Logging;
using chibild.Internal;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace chibild.Generating;

internal sealed class AssemblyInputFragment :
    InputFragment
{
    private readonly Dictionary<string, TypeDefinition> types;
    private readonly Dictionary<string, FieldDefinition> fields;
    private readonly Dictionary<string, MethodDefinition[]> methods;
    private readonly Dictionary<string, ModuleDefinition> resolvedModules = new();
    
    private AssemblyInputFragment(
        string baseInputPath,
        string relativePath,
        AssemblyDefinition assembly,
        Dictionary<string, TypeDefinition> types,
        Dictionary<string, FieldDefinition> fields,
        Dictionary<string, MethodDefinition[]> methods) :
        base(baseInputPath, relativePath)
    {
        this.Assembly = assembly;
        this.types = types;
        this.fields = fields;
        this.methods = methods;
    }

    public AssemblyDefinition Assembly { get; }

    public override string ToString() =>
        $"Assembly: {this.ObjectPath}";

    //////////////////////////////////////////////////////////////

    public override bool ContainsTypeAndSchedule(
        TypeNode type,
        out Scopes scope)
    {
        if (this.types.TryGetValue(type.TypeIdentity, out _))
        {
            scope = Scopes.Public;
            return true;
        }
        scope = default;
        return false;
    }

    public override bool ContainsVariableAndSchedule(
        IdentityNode variable) =>
        this.fields.ContainsKey(variable.Identity);

    private static bool TryGetMatchedMethodIndex(
        FunctionSignatureNode signature,
        MethodDefinition[] overloads,
        out int index)
    {
        // If target signature is variadic, will ignore exact match.
        if (signature.CallingConvention ==
            chibicc.toolchain.Parsing.MethodCallingConvention.Default)
        {
            for (index = 0; index < overloads.Length; index++)
            {
                var overload = overloads[index];

                // Match exactly.
                if (overload.Parameters.
                    Select(p => p.ParameterType.FullName).
                    SequenceEqual(signature.Parameters.Select(p => p.ParameterType.CilTypeName)))
                {
                    return true;
                }
            }
        }

        for (index = 0; index < overloads.Length; index++)
        {
            var overload = overloads[index];

            // Match partially when overload is variadic.
            if (overload.CallingConvention == Mono.Cecil.MethodCallingConvention.VarArg)
            {
                if (overload.Parameters.
                    Select(p => p.ParameterType.FullName).
                    SequenceEqual(signature.Parameters.
                        Take(overload.Parameters.Count).
                        Select(p => p.ParameterType.CilTypeName)))
                {
                    return true;
                }
            }
            if (signature.CallingConvention ==
                chibicc.toolchain.Parsing.MethodCallingConvention.VarArg)
            {
                if (overload.Parameters.
                    Take(signature.Parameters.Length).
                    Select(p => p.ParameterType.FullName).
                    SequenceEqual(signature.Parameters.
                        Select(p => p.ParameterType.CilTypeName)))
                {
                    return true;
                }
            }
        }

        index = -1;
        return false;
    }

    public override bool ContainsFunctionAndSchedule(
        IdentityNode function,
        FunctionSignatureNode? signature) =>
        this.methods.TryGetValue(function.Identity, out var overloads) &&
        (signature == null || TryGetMatchedMethodIndex(signature, overloads, out _));

    //////////////////////////////////////////////////////////////

    private ModuleDefinition ResovleOnFallbackModule(
        ModuleDefinition fallbackModule, MemberReference mr)
    {
        var anr = mr.Module.Assembly.Name;
        if (!this.resolvedModules.TryGetValue(anr.Name, out var module))
        {
            var assembly = fallbackModule.AssemblyResolver.Resolve(anr);
            module = assembly.MainModule;
            this.resolvedModules.Add(anr.Name, module);
        }
        return module;
    }

    public override bool TryGetType(
        TypeNode type,
        ModuleDefinition fallbackModule,
        out TypeReference tr)
    {
        if (this.types.TryGetValue(type.TypeIdentity, out var td))
        {
            if (td.Module == fallbackModule)
            {
                tr = td;
                return true;
            }

            // Resolve on fallback assembly resolver.
            var exactModule = this.ResovleOnFallbackModule(fallbackModule, td);
            if (exactModule.GetType(td.FullName) is { } ftd)
            {
                this.types[type.TypeIdentity] = ftd;

                tr = ftd;
                return true;
            }
            else
            {
                Debug.Fail($"Could not resolve a type on fallback assembly: {td.FullName}");
            }
        }

        tr = null!;
        return false;
    }

    public override bool TryGetField(
        IdentityNode variable,
        ModuleDefinition fallbackModule,
        out FieldReference fr)
    {
        if (this.fields.TryGetValue(variable.Identity, out var fd))
        {
            if (fd.Module == fallbackModule)
            {
                fr = fd;
                return true;
            }

            // Resolve on fallback assembly resolver.
            var exactModule = this.ResovleOnFallbackModule(fallbackModule, fd);
            if (exactModule.GetType(fd.DeclaringType.FullName) is { } ftd &&
                ftd.Fields.FirstOrDefault(f => f.Name == fd.Name) is { } ffd)
            {
                this.fields[variable.Identity] = ffd;

                fr = ffd;
                return true;
            }
            else
            {
                Debug.Fail($"Could not resolve a field on fallback assembly: {fd.DeclaringType.FullName}.{fd.Name}");
            }
        }

        fr = null!;
        return false;
    }

    public override bool TryGetMethod(
        IdentityNode function,
        FunctionSignatureNode? signature,
        ModuleDefinition fallbackModule,
        out MethodReference mr)
    {
        if (!this.methods.TryGetValue(function.Identity, out var overloads))
        {
            mr = null!;
            return false;
        }

        // Resolve on fallback assembly resolver.
        MethodReference ResolveOnFallbackModule(MethodDefinition md)
        {
            var exactModule = this.ResovleOnFallbackModule(fallbackModule, md);
            if (exactModule.GetType(md.DeclaringType.FullName) is { } ftd &&
                ftd.Methods.FirstOrDefault(m => CecilUtilities.Equals(m, md)) is { } fmd)
            {
                return fmd;
            }
            else
            {
                Debug.Fail($"Could not resolve a method on fallback assembly: {md.DeclaringType.FullName}.{md.Name}({signature})");
                return md;
            }
        }

        if (signature == null)
        {
            mr = ResolveOnFallbackModule(overloads[0]);
            return true;
        }
        if (TryGetMatchedMethodIndex(signature, overloads, out var index))
        {
            mr = ResolveOnFallbackModule(overloads[index]);
            return true;
        }

        mr = null!;
        return false;
    }
    
    //////////////////////////////////////////////////////////////

    public static AssemblyInputFragment Load(
        ILogger logger,
        string baseInputPath,
        string relativePath,
        CachedAssemblyResolver assemblyResolver)
    {
        // TODO: native dll

        logger.Information($"Loading assembly: {relativePath}");

        var assembly = assemblyResolver.ReadAssemblyFrom(
            Path.Combine(baseInputPath, relativePath));

        static IEnumerable<TypeDefinition> IterateTypesDescendants(TypeDefinition type)
        {
            yield return type;

            foreach (var childType in type.NestedTypes.Where(nestedType =>
                nestedType.IsNestedPublic &&
                (nestedType.IsClass || nestedType.IsInterface || nestedType.IsValueType || nestedType.IsEnum) &&
                // Excepts all generic types because CABI does not support it.
                !nestedType.HasGenericParameters).
                SelectMany(IterateTypesDescendants))
            {
                yield return childType;
            }
        }

        var targetTypes = assembly.Modules.
            SelectMany(module => module.Types).
            Where(type =>
                type.IsPublic &&
                (type.IsClass || type.IsInterface || type.IsValueType || type.IsEnum) &&
                // Excepts all generic types because CABI does not support it.
                !type.HasGenericParameters).
            SelectMany(IterateTypesDescendants).
            ToArray();

        var types = targetTypes.
            // Combine both CABI types and .NET types.
            Where(type => type.Namespace is "C.type").
            Select(type => (name: type.Name, type)).
            Concat(targetTypes.
                Select(type => (name: type.FullName, type))).
            ToDictionary(entry => entry.name, entry => entry.type);

        var targetFields = targetTypes.
            Where(type => type is
            {
                IsPublic: true, IsClass: true,
            } or
            {
                IsPublic: true, IsValueType: true, IsEnum: false,
            }).
            SelectMany(type => type.Fields).
            Where(field => field is
            {
                IsPublic: true,
            }).
            ToArray();

        var fields = targetFields.
            // Combine both CABI variables and .NET fields.
            Where(field => field.DeclaringType.FullName is "C.data" or "C.rdata").
            Select(field => (name: field.Name, field)).
            Concat(targetFields.
                Select(field => (name: $"{field.DeclaringType.FullName}.{field.Name}", field))).
            ToDictionary(entry => entry.name, entry => entry.field);

        var targetMethods = targetTypes.
            Where(type => type is
            {
                IsPublic: true, IsClass: true,
            } or
            {
                IsPublic: true, IsValueType: true,
            }).
            SelectMany(type => type.Methods).
            Where(method => method is
            {
                IsPublic: true,
                // Excepts all generic methods because CABI does not support it.
                HasGenericParameters: false
            }).
            ToArray();

        var methods = targetMethods.
            // Combine both CABI function and .NET methods.
            Where(method =>
                method.IsStatic &&
                method.DeclaringType.FullName is "C.text").
            Select(method => (name: method.Name, method)).
            Concat(targetMethods.
                Select(method =>
                (
                    name: $"{method.DeclaringType.FullName}.{method.Name}",
                    method
                ))).
            GroupBy(
                entry => entry.name,
                entry => entry.method).
            ToDictionary(
                g => g.Key,
                // Sorted descending longer parameters.
                g => g.OrderByDescending(method => method.Parameters.Count).ToArray());

        return new(
            baseInputPath,
            relativePath,
            assembly,
            types,
            fields,
            methods);
    }
}
