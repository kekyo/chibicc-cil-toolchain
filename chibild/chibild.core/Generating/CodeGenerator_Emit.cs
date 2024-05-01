/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Logging;
using chibicc.toolchain.Parsing;
using chibicc.toolchain.Tokenizing;
using chibild.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace chibild.Generating;

partial class CodeGenerator
{
    private void AddFundamentalAttributes(
        InputFragment[] inputFragments,
        TargetFramework? targetFramework,
        bool? disableJITOptimization)
    {
        bool UnsafeGetCoreType(
            string coreTypeName,
            out TypeReference tr)
        {
            var coreType = new TypeIdentityNode(coreTypeName, Token.Unknown);
            foreach (var fragment in inputFragments)
            {
                if (fragment.TryGetType(
                    coreType,
                    this.targetModule,
                    out tr))
                {
                    return true;
                }
            }
            tr = null!;
            return false;
        }
        
        // Apply TargetFrameworkAttribute if could be imported.
        if (targetFramework != null)
        {
            if (UnsafeGetCoreType(
                "System.Runtime.Versioning.TargetFrameworkAttribute",
                out var tfatr))
            {
                if (tfatr.Resolve().Methods.FirstOrDefault(m =>
                    m is { IsPublic: true, IsStatic: false, IsConstructor: true, } &&
                    m.Parameters.Count == 1 &&
                    m.Parameters[0].ParameterType.FullName == "System.String") is MethodReference ctor)
                {
                    ctor = this.targetModule.SafeImport(ctor);
                    
                    var tfa = new CustomAttribute(ctor);
                    tfa.ConstructorArguments.Add(new(
                        this.targetModule.TypeSystem.String,
                        targetFramework.ToString()));
                    this.targetModule.Assembly.CustomAttributes.Add(tfa);

                    this.logger.Trace(
                        "TargetFrameworkAttribute is applied.");
                }
                else
                {
                    this.logger.Warning(
                        "TargetFrameworkAttribute constructor was not found.");
                }
            }
            else
            {
                this.logger.Warning(
                    "TargetFrameworkAttribute was not found, so not applied. Because maybe did not reference core library.");
            }
        }

        // Apply DebuggableAttribute if could be imported.
        if (disableJITOptimization ?? false)
        {
            if (UnsafeGetCoreType(
                "System.Diagnostics.DebuggableAttribute.DebuggingModes",
                out var dadmtr) &&
                UnsafeGetCoreType(
                "System.Diagnostics.DebuggableAttribute",
                out var datr))
            {
                if (datr.Resolve().Methods.FirstOrDefault(m =>
                    m is { IsPublic: true, IsStatic: false, IsConstructor: true, } &&
                    m.Parameters.Count == 1 &&
                    m.Parameters[0].ParameterType.FullName ==
                    "System.Diagnostics.DebuggableAttribute.DebuggingModes") is MethodReference ctor)
                {
                    dadmtr = this.targetModule.SafeImport(dadmtr);
                    ctor = this.targetModule.SafeImport(ctor);
                    
                    var da = new CustomAttribute(ctor);
                    da.ConstructorArguments.Add(new(
                        dadmtr,
                        (int)(DebuggableAttribute.DebuggingModes.Default |
                              DebuggableAttribute.DebuggingModes.DisableOptimizations)));
                    this.targetModule.Assembly.CustomAttributes.Add(da);

                    this.logger.Trace(
                        "DebuggableAttribute is applied.");
                }
                else
                {
                    this.logger.Warning(
                        "DebuggableAttribute constructor was not found.");
                }
            }
            else
            {
                this.logger.Warning(
                    "DebuggableAttribute and/or DebuggingModes was not found, so not applied. Because maybe did not reference core library.");
            }
        }

        // Apply pointer visualizers if could be imported.
        if (UnsafeGetCoreType(
                "System.Type",
                out var sttr) &&
            UnsafeGetCoreType(
                "System.Diagnostics.DebuggerTypeProxyAttribute",
                out var dtpatr) &&
            UnsafeGetCoreType(
                "C.type.__pointer_visualizer",
                out var pvtr))
        {
            if (dtpatr.Resolve().Methods.FirstOrDefault(m =>
                m is { IsPublic: true, IsStatic: false, IsConstructor: true, } &&
                m.Parameters.Count == 1 &&
                m.Parameters[0].ParameterType.FullName ==
                "System.Type") is MethodReference ctor)
            {
                sttr = this.targetModule.SafeImport(sttr);
                pvtr = this.targetModule.SafeImport(pvtr);
                ctor = this.targetModule.SafeImport(ctor);

                foreach (var targetTypeName in new[]
                    { "System.Void", "System.Byte", "System.SByte", })
                {
                    if (UnsafeGetCoreType(
                        targetTypeName,
                        out var ttr))
                    {
                        var ptr = this.targetModule.SafeImport(ttr).MakePointerType();
                        
                        var dtpa = new CustomAttribute(ctor);
                        dtpa.ConstructorArguments.Add(new(sttr, pvtr));
                        dtpa.Properties.Add(new(
                            "Target",
                            new(sttr, ptr)));
                        this.targetModule.Assembly.CustomAttributes.Add(dtpa);

                        this.logger.Trace(
                            $"DebuggerTypeProxyAttribute({ptr.FullName}) is applied.");
                    }
                    else
                    {
                        this.logger.Warning(
                            "DebuggerTypeProxyAttribute was not found, so not applied. Because maybe did not reference core library.");
                    }
                }
            }
            else
            {
                this.logger.Warning(
                    "DebuggerTypeProxyAttribute and/or pointer visualizer type was not found, so not applied. Because maybe did not reference core library or libc.");
            }
        }
    }

    private void AssignEntryPoint(
        string entryPointSymbol)
    {
        // Priority search for a module type.
        var startup = this.targetModule.Types.
            First(t => t.FullName == "<Module>").
            Methods.
            FirstOrDefault(m =>
                m.IsStatic &&
                m.Name == entryPointSymbol);

        // Entire search when not found.
        if (startup == null)
        {
            startup = this.targetModule.Types.
                Where(type => type.IsClass).
                SelectMany(type => type.Methods).
                FirstOrDefault(m =>
                    m.IsStatic &&
                    m.Name == entryPointSymbol);
        }

        // Inject startup code when declared.
        if (startup != null)
        {
            this.targetModule.EntryPoint = startup;
            this.logger.Information($"Found entry point.");
        }
        else
        {
            this.caughtError = true;
            this.logger.Error($"Could not find any entry point.");
        }
    }

    private sealed class TypeDefinitionHolder
    {
        private readonly Lazy<TypeDefinition> fileScopedType;
        private readonly Lazy<TypeDefinition> dataType;
        private readonly Lazy<TypeDefinition> rdataType;
        private readonly Lazy<TypeDefinition> textType;
        private readonly Lazy<MethodBody> fileScopedInitializerBody;
        private readonly Lazy<MethodBody> dataInitializerBody;

        public TypeDefinitionHolder(
            ObjectInputFragment fragment,
            ModuleDefinition targetModule)
        {
            this.fileScopedType = new(() =>
            {
                var fileTypeName = $"<{CecilUtilities.SanitizeFileNameToMemberName(fragment.ObjectName)}>$";
                if (targetModule.GetType(fileTypeName) is not { } type)
                {
                    type = new("", fileTypeName,
                        TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                        targetModule.TypeSystem.Object);
                    targetModule.Types.Add(type);
                }
                return type;
            });
            this.dataType = new(() =>
            {
                if (targetModule.GetType("C.data") is not { } type)
                {
                    type = new("C", "data",
                        TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                        targetModule.TypeSystem.Object);
                    targetModule.Types.Add(type);
                }
                return type;
            });
            this.rdataType = new(() =>
            {
                if (targetModule.GetType("C.rdata") is not { } type)
                {
                    type = new("C", "rdata",
                        TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                        targetModule.TypeSystem.Object);
                    targetModule.Types.Add(type);
                }
                return type;
            });
            this.textType = new(() =>
            {
                if (targetModule.GetType("C.text") is not { } type)
                {
                    type = new("C", "text",
                        TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
                        targetModule.TypeSystem.Object);
                    targetModule.Types.Add(type);
                }
                return type;
            });

            MethodBody GetInitializerBody(TypeDefinition type)
            {
                if (type.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic) is not { HasBody: true } cctor)
                {
                    cctor = new MethodDefinition(
                        ".cctor",
                        MethodAttributes.Private | MethodAttributes.Static |
                        MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
                        targetModule.TypeSystem.Void);

                    var body = cctor.Body;
                    body.InitLocals = false;
                    body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                    type.Methods.Add(cctor);
                }
                return cctor.Body;
            }

            this.fileScopedInitializerBody = new(() =>
                GetInitializerBody(this.fileScopedType.Value));
            this.dataInitializerBody = new(() =>
                GetInitializerBody(this.dataType.Value));
        }

        public TypeDefinition GetFileScopedType() =>
            this.fileScopedType.Value;

        public TypeDefinition GetDataType() =>
            this.dataType.Value;

        public TypeDefinition GetRDataType() =>
            this.rdataType.Value;

        public TypeDefinition GetTextType() =>
            this.textType.Value;

        public Mono.Collections.Generic.Collection<Instruction> GetFileScopedInitializerBody() =>
            this.fileScopedInitializerBody.Value.Instructions;

        public Mono.Collections.Generic.Collection<Instruction> GetDataInitializerBody() =>
            this.dataInitializerBody.Value.Instructions;
    }

    private void EmitMembers(
        ObjectInputFragment fragment)
    {
        var holder = new TypeDefinitionHolder(
            fragment,
            this.targetModule);

        ///////////////////////////////////////////////////

        foreach (var enumeration in fragment.GetDeclaredEnumerations(true))
        {
            holder.GetFileScopedType().
                NestedTypes.
                Add(enumeration);
        }

        foreach (var structure in fragment.GetDeclaredStructures(true))
        {
            holder.GetFileScopedType().
                NestedTypes.
                Add(structure);
        }

        foreach (var function in fragment.GetDeclaredFunctions(true))
        {
            holder.GetFileScopedType().
                Methods.
                Add(function);
        }

        foreach (var variable in fragment.GetDeclaredVariables(true))
        {
            holder.GetFileScopedType().
                Fields.
                Add(variable);
        }

        foreach (var constant in fragment.GetDeclaredConstants(true))
        {
            holder.GetFileScopedType().
                Fields.
                Add(constant);
        }

        foreach (var initializer in fragment.GetDeclaredInitializer(true).Reverse())
        {
            holder.GetFileScopedType().
                Methods.
                Insert(0, initializer);

            // Insert type initializer caller.
            var instructions = holder.GetFileScopedInitializerBody();
            instructions.Insert(
                0,
                Instruction.Create(OpCodes.Call, initializer));
        }

        ///////////////////////////////////////////////////

        foreach (var enumeration in fragment.GetDeclaredEnumerations(false))
        {
            this.targetModule.Types.Add(enumeration);
        }

        foreach (var structure in fragment.GetDeclaredStructures(false))
        {
            this.targetModule.Types.Add(structure);
        }

        foreach (var function in fragment.GetDeclaredFunctions(false))
        {
            holder.GetTextType().
                Methods.
                Add(function);
        }

        foreach (var variable in fragment.GetDeclaredVariables(false))
        {
            holder.GetDataType().
                Fields.
                Add(variable);
        }

        foreach (var constant in fragment.GetDeclaredConstants(false))
        {
            holder.GetRDataType().
                Fields.
                Add(constant);
        }

        foreach (var initializer in fragment.GetDeclaredInitializer(false).Reverse())
        {
            holder.GetDataType().
                Methods.
                Insert(0, initializer);

            // Insert type initializer caller.
            var instructions = holder.GetDataInitializerBody();
            instructions.Insert(
                0,
                Instruction.Create(OpCodes.Call, initializer));
        }

        ///////////////////////////////////////////////////

        var moduleType = this.targetModule.Types.
            First(t => t.FullName == "<Module>");

        foreach (var function in fragment.GetDeclaredModuleFunctions())
        {
            moduleType.
                Methods.
                Add(function);
        }
    }

    private void OptimizeMethods(
        ObjectInputFragment fragment)
    {
        foreach (var function in fragment.GetDeclaredFunctions(true))
        {
            function.Body.Optimize();
        }

        foreach (var function in fragment.GetDeclaredFunctions(false))
        {
            function.Body.Optimize();
        }

        foreach (var initializer in fragment.GetDeclaredInitializer(true))
        {
            initializer.Body.Optimize();
        }

        foreach (var initializer in fragment.GetDeclaredInitializer(false))
        {
            initializer.Body.Optimize();
        }
    }

    public bool Emit(
        InputFragment[] inputFragments,
        bool applyOptimization,
        TargetFramework? targetFramework,
        bool? disableJITOptimization,
        string? entryPointSymbol)
    {
        // Try add fundamental attributes.
        this.AddFundamentalAttributes(
            inputFragments,
            targetFramework,
            disableJITOptimization);

        // Combine all object fragments into target module.
        foreach (var fragment in inputFragments.
            OfType<ObjectInputFragment>())
        {
            this.EmitMembers(fragment);
        }

        // Apply all delayed looking up (types).
        while (this.delayLookingUpEntries1.Count >= 1)
        {
            var action = this.delayLookingUpEntries1.Dequeue();
            action();
        }

        // Apply all delayed looking up (not types).
        while (this.delayLookingUpEntries2.Count >= 1)
        {
            var action = this.delayLookingUpEntries2.Dequeue();
            action();
        }

        // Assert reverse dependencies are nothing.
        Debug.Assert(this.delayLookingUpEntries1.Count == 0);

        // Apply method optimization for all object fragments.
        if (applyOptimization)
        {
            foreach (var fragment in inputFragments.
                OfType<ObjectInputFragment>())
            {
                this.OptimizeMethods(fragment);
            }
        }

        // Apply all delayed debugger information.
        if (this.produceDebuggingInformation)
        {
            var cachedDocuments = new Dictionary<string, Document>();
            while (this.delayDebuggingInsertionEntries.Count >= 1)
            {
                var action = this.delayDebuggingInsertionEntries.Dequeue();
                action(cachedDocuments);
            }
        }
        else
        {
            Debug.Assert(this.delayDebuggingInsertionEntries.Count == 0);
        }

        // Assign entry point when set producing executable.
        if (entryPointSymbol != null)
        {
            this.AssignEntryPoint(entryPointSymbol);
        }

        // (Completed all CIL implementations in this place.)

        ///////////////////////////////////////////////

        return !this.caughtError;
    }
}
