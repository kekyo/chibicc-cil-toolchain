﻿/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace chibild;

partial class LinkerTests
{
    private string Run(
        string[] chibildSourceCodes,
        string[]? additionalReferencePaths = null,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        string targetFrameworkMoniker = "net45",
        string[]? prependExecutionSearchPaths = null,
         [CallerMemberName] string memberName = null!) =>
        LinkerTestRunner.RunCore(
            chibildSourceCodes,
            additionalReferencePaths,
            null,
            prependExecutionSearchPaths,
            () =>
            {
                var appHostTemplatePath = Path.GetFullPath(
                    Path.Combine(
                        LinkerTestRunner.ArtifactsBasePath,
                        CommonUtilities.IsInWindows ? "apphost.exe" : "apphost.linux-x64"));
                var tf = TargetFramework.TryParse(targetFrameworkMoniker, out var tf1) ?
                    tf1 : throw new InvalidOperationException();
                return new()
                {
                    AssemblyOptions = AssemblyOptions.None,
                    AssemblyType = assemblyType,
                    TargetFramework = tf,
                    AppHostTemplatePath = appHostTemplatePath,
                };
            },
            memberName);

    private string Run(
        string chibildSourceCode,
        string[]? additionalReferencePaths = null,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        string targetFrameworkMoniker = "net45",
        string[]? prependExecutionSearchPaths = null,
        [CallerMemberName] string memberName = null!) =>
        this.Run(
            new[] { chibildSourceCode },
            additionalReferencePaths,
            assemblyType,
            targetFrameworkMoniker,
            prependExecutionSearchPaths,
            memberName);

    private string RunInjection(
        string chibildSourceCode,
        string injectToAssemblyPath,
        string[]? additionalReferencePaths = null,
        [CallerMemberName] string memberName = null!) =>
        LinkerTestRunner.RunCore(
            new[] { chibildSourceCode },
            additionalReferencePaths,
            injectToAssemblyPath,
            null,
            () => null,
            memberName);
}
