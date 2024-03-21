/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibias.Internal;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace chibias;

partial class AssemblerTests
{
    private string Run(
        string[] chibiasSourceCodes,
        string[]? additionalReferencePaths = null,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        string targetFrameworkMoniker = "net45",
        [CallerMemberName] string memberName = null!) =>
        AssemblerTestRunner.RunCore(
            chibiasSourceCodes,
            additionalReferencePaths,
            null,
            () =>
            {
                var appHostTemplatePath = Path.GetFullPath(
                    Path.Combine(
                        AssemblerTestRunner.ArtifactsBasePath,
                        Utilities.IsInWindows ? "apphost.exe" : "apphost.linux-x64"));
                var tf = TargetFramework.TryParse(targetFrameworkMoniker, out var tf1) ?
                    tf1 : throw new InvalidOperationException();
                return new()
                {
                    Options = AssembleOptions.None,
                    AssemblyType = assemblyType,
                    TargetFramework = tf,
                    AppHostTemplatePath = appHostTemplatePath,
                };
            },
            memberName);

    private string Run(
        string chibiasSourceCode,
        string[]? additionalReferencePaths = null,
        AssemblyTypes assemblyType = AssemblyTypes.Dll,
        string targetFrameworkMoniker = "net45",
        [CallerMemberName] string memberName = null!) =>
        this.Run(
            new[] { chibiasSourceCode },
            additionalReferencePaths,
            assemblyType,
            targetFrameworkMoniker,
            memberName);

    private string RunInjection(
        string chibiasSourceCode,
        string injectToAssemblyPath,
        string[]? additionalReferencePaths = null,
        [CallerMemberName] string memberName = null!) =>
        AssemblerTestRunner.RunCore(
            new[] { chibiasSourceCode },
            additionalReferencePaths,
            injectToAssemblyPath,
            () => null,
            memberName);
}
