/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DiffEngine;

namespace chibiar;

internal static class ArchiverTestRunner
{
    static ArchiverTestRunner()
    {
        DiffTools.UseOrder(
            DiffTool.WinMerge,
            DiffTool.Meld,
            DiffTool.VisualStudio,
            DiffTool.Rider);        
    }
    
    public static readonly string ArtifactsBasePath = Path.GetFullPath(
        Path.Combine("..", "..", "..", "artifacts"));
    
    private static readonly string id =
        $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{new Random().Next()}";

    public static Task RunAsync(
        Func<string, Task> tester,
        [CallerMemberName] string memberName = null!)
    {
        var basePath = Path.GetFullPath(
            Path.Combine("tests", id, memberName));

        Directory.CreateDirectory(basePath);

        return tester(basePath);
    }
}
