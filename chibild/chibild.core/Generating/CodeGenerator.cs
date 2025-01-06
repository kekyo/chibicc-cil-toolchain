/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Logging;
using chibicc.toolchain.Tokenizing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace chibild.Generating;

internal sealed partial class CodeGenerator
{
    // Cecil probably does not support multi-threaded access.
    // To speed up chibild, mutable accesses to `ModuleDefinition`
    // (e.g., collection operations such as adding types and members)
    // are delayed until the final stage `ConsumeFinal()`,
    // during which time multithreading speedup is achieved.

    private readonly ILogger logger;
    private readonly ModuleDefinition targetModule;
    private readonly bool produceDebuggingInformation;

    private readonly Queue<Action> delayLookingUpEntries1 = new();
    private readonly Queue<Action> delayLookingUpEntries2 = new();
    private readonly Queue<Action<Dictionary<string, Document>, bool>> delayDebuggingInsertionEntries = new();

    private bool caughtError;
    private int placeholderIndex;

    public CodeGenerator(
        ILogger logger,
        ModuleDefinition targetModule,
        bool produceDebuggingInformation)
    {
        this.logger = logger;
        this.targetModule = targetModule;
        this.produceDebuggingInformation = produceDebuggingInformation;
    }
    
    //////////////////////////////////////////////////////////////

    private void OutputError(Token token, string message)
    {
        this.caughtError = true;
        this.logger.Error(
            $"{token.RelativePath}:{token.Line + 1}:{token.StartColumn + 1}: {message}");
    }

    private void OutputWarning(Token token, string message)
    {
        this.logger.Warning(
            $"{token.RelativePath}:{token.Line + 1}:{token.StartColumn + 1}: {message}");
    }

    //////////////////////////////////////////////////////////////

    private void ConsumeFragment(
        ObjectInputFragment currentFragment,
        InputFragment[] inputFragments)
    {
        using var scope = this.logger.BeginScope(LogLevels.Debug);

        var context = new LookupContext(
            this.targetModule,
            currentFragment,
            inputFragments);

#if DEBUG
        foreach (var variable in currentFragment.GlobalVariables)
        {
            this.ConsumeGlobalVariable(context, variable);
        }
        scope.Debug($"[1]: {currentFragment.ObjectName}");

        foreach (var constant in currentFragment.GlobalConstants)
        {
            this.ConsumeGlobalConstant(context, constant);
        }
        scope.Debug($"[2]: {currentFragment.ObjectName}");

        foreach (var function in currentFragment.Functions)
        {
            this.ConsumeFunction(context, function);
        }
        scope.Debug($"[3]: {currentFragment.ObjectName}");

        foreach (var initializer in currentFragment.Initializers)
        {
            this.ConsumeInitializer(context, initializer);
        }
        scope.Debug($"[4]: {currentFragment.ObjectName}");

        foreach (var enumeration in currentFragment.Enumerations)
        {
            this.ConsumeEnumeration(context, enumeration);
        }
        scope.Debug($"[5]: {currentFragment.ObjectName}");

        foreach (var structure in currentFragment.Structures)
        {
            this.ConsumeStructure(context, structure);
        }
        scope.Debug($"[6]: {currentFragment.ObjectName}");
#else
        Parallel.Invoke(
            () =>
            {
                foreach (var variable in currentFragment.GlobalVariables)
                {
                    this.ConsumeGlobalVariable(context, variable);
                }
                scope.Debug($"[1]: {currentFragment.ObjectName}");
            },
            () =>
            {
                foreach (var constant in currentFragment.GlobalConstants)
                {
                    this.ConsumeGlobalConstant(context, constant);
                }
                scope.Debug($"[2]: {currentFragment.ObjectName}");
            },
            () =>
            {
                foreach (var function in currentFragment.Functions)
                {
                    this.ConsumeFunction(context, function);
                }
                scope.Debug($"[3]: {currentFragment.ObjectName}");
            },
            () =>
            {
                foreach (var initializer in currentFragment.Initializers)
                {
                    this.ConsumeInitializer(context, initializer);
                }
                scope.Debug($"[4]: {currentFragment.ObjectName}");
            },
            () =>
            {
                foreach (var enumeration in currentFragment.Enumerations)
                {
                    this.ConsumeEnumeration(context, enumeration);
                }
                scope.Debug($"[5]: {currentFragment.ObjectName}");
            },
            () =>
            {
                foreach (var structure in currentFragment.Structures)
                {
                    this.ConsumeStructure(context, structure);
                }
                scope.Debug($"[6]: {currentFragment.ObjectName}");
            });
#endif
    }

    public void Clear()
    {
        this.delayLookingUpEntries1.Clear();
        this.delayLookingUpEntries2.Clear();
        this.delayDebuggingInsertionEntries.Clear();
        this.placeholderIndex = 0;
        this.caughtError = false;
    }

    private void ConsumeArchivedObject(
        InputFragment[] inputFragments,
        bool isLocationOriginSource)
    {
        bool found;
        do
        {
            found = false;
#if DEBUG
            foreach (var currentFragment in inputFragments.
                OfType<ArchivedObjectInputFragment>())
            {
                switch (currentFragment.LoadObjectIfRequired(
                    this.logger,
                    isLocationOriginSource))
                {
                    case ArchivedObjectInputFragment.LoadObjectResults.Loaded:
                        found = true;
                        this.ConsumeFragment(currentFragment, inputFragments);
                        break;
                    case ArchivedObjectInputFragment.LoadObjectResults.Ignored:
                        break;
                    default:
                        this.caughtError = true;
                        break;
                }
            }
#else
            Parallel.ForEach(inputFragments,
                new() { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                currentFragment =>
                {
                    if (currentFragment is ArchivedObjectInputFragment afif)
                    {
                        switch (afif.LoadObjectIfRequired(
                            this.logger,
                            isLocationOriginSource))
                        {
                            case ArchivedObjectInputFragment.LoadObjectResults.Loaded:
                                found = true;
                                this.ConsumeFragment(afif, inputFragments);
                                break;
                            case ArchivedObjectInputFragment.LoadObjectResults.Ignored:
                                break;
                            default:
                                this.caughtError = true;
                                break;
                        }
                    }
                });
#endif
        }
        while (found && !this.caughtError);
    }

    public bool ConsumeInputs(
        InputFragment[] inputFragments,
        bool isLocationOriginSource)
    {
        using var scope = this.logger.BeginScope(LogLevels.Debug);
        scope.Debug($"InputFragments={inputFragments.Length}");
        
        ////////////////////////////////////
        // Step 1. Consume all objects.

#if DEBUG
        foreach (var currentFragment in inputFragments.
            OfType<ObjectInputFragment>().
            Where(fragment => fragment is not ArchivedObjectInputFragment))
        {
            this.ConsumeFragment(currentFragment, inputFragments);
        }
#else
        Parallel.ForEach(inputFragments,
            new() { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
            currentFragment =>
            {
                if (currentFragment is ObjectInputFragment ofif &&
                    ofif is not ArchivedObjectInputFragment)
                {
                    this.ConsumeFragment(ofif, inputFragments);
                }
            });
#endif
        if (this.caughtError)
        {
            return false;
        }

        scope.Debug("Step 2");

        ////////////////////////////////////
        // Step 2. Consume scheduled object referring in archives.
        
        this.ConsumeArchivedObject(
            inputFragments,
            isLocationOriginSource);

        scope.Debug("Finished");

        return !this.caughtError;
    }
}
