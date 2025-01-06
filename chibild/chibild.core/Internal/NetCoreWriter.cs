/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using chibicc.toolchain.Internal;
using chibicc.toolchain.Logging;
using System.Diagnostics;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using chibicc.toolchain.IO;

namespace chibild.Internal;

internal static class NetCoreWriter
{
    private static readonly string runtimeConfigJsonTemplate =
        StreamUtilities.CreateTextReader(typeof(CilLinker).Assembly.GetManifestResourceStream(
            "chibild.Internal.runtimeconfig.json")!).
        ReadToEnd();

    private static readonly byte[] appBinaryPathPlaceholderSearchValue =
        Encoding.UTF8.GetBytes("c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2");

    ////////////////////////////////////////////////////////////////////////////////////

    private static string GetRollForwardValue(RuntimeConfigurationOptions option) =>
        option switch
        {
            RuntimeConfigurationOptions.ProduceCoreCLRMajorRollForward => "major",
            RuntimeConfigurationOptions.ProduceCoreCLRMinorRollForward => "minor",
            RuntimeConfigurationOptions.ProduceCoreCLRFeatureRollForward => "feature",
            RuntimeConfigurationOptions.ProduceCoreCLRPatchRollForward => "patch",
            RuntimeConfigurationOptions.ProduceCoreCLRLatestMajorRollForward => "latestMajor",
            RuntimeConfigurationOptions.ProduceCoreCLRLatestMinorRollForward => "latestMinor",
            RuntimeConfigurationOptions.ProduceCoreCLRLatestFeatureRollForward => "latestFeature",
            RuntimeConfigurationOptions.ProduceCoreCLRDisableRollForward => "disable",
            _ => throw new ArgumentException(),
        };

    public static void WriteRuntimeConfiguration(
        ILogger logger,
        string outputAssemblyCandidateFullPath,
        LinkerOptions options)
    {
        var co = options.CreationOptions;

        Debug.Assert(co != null);

        var runtimeConfigJsonPath = Path.Combine(
            CommonUtilities.GetDirectoryPath(outputAssemblyCandidateFullPath),
            Path.GetFileNameWithoutExtension(outputAssemblyCandidateFullPath) + ".runtimeconfig.json");

        logger.Information(
            $"Writing: {Path.GetFileName(runtimeConfigJsonPath)}");

        using var tw = File.CreateText(runtimeConfigJsonPath);

        var sb = new StringBuilder(runtimeConfigJsonTemplate);
        sb.Replace("{tfm}", co!.TargetFramework.Moniker);
        if (co.RuntimeConfiguration ==
            RuntimeConfigurationOptions.ProduceCoreCLR)
        {
            sb.Replace("{rollForward}", "");
        }
        else
        {
            sb.Replace(
                "{rollForward}",
                $"\"rollForward\": \"{GetRollForwardValue(co.RuntimeConfiguration)}\",{Environment.NewLine}    ");
        }
        if (co.TargetFramework.Version.Build >= 0)
        {
            sb.Replace("{tfv}", co.TargetFramework.Version.ToString(3));
        }
        else
        {
            sb.Replace("{tfv}", co.TargetFramework.Version.ToString(2) + ".0");
        }

        tw.Write(sb.ToString());
        tw.Flush();
    }

    ////////////////////////////////////////////////////////////////////////////////////

    public static void WriteAppHost(
        ILogger logger,
        string outputAssemblyFullPath,
        string outputAssemblyPath,
        string appHostTemplateFullPath,
        LinkerOptions options)
    {
        // .NET Core AppHost to perform the necessary processing to make it function.
        // Most of this is likely to be necessary to avoid Windows-specific problems.

        using var ms = new MemoryStream();
        using (var fs = new FileStream(
            appHostTemplateFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fs.CopyTo(ms);
        }
        ms.Position = 0;

        var outputAssemblyName = Path.GetFileName(outputAssemblyPath);
        var outputAssemblyNameBytes = Encoding.UTF8.GetBytes(outputAssemblyName);

        var isPEImage = PEUtils.IsPEImage(ms);
        var outputFullPath = Path.Combine(
            CommonUtilities.GetDirectoryPath(outputAssemblyFullPath),
            Path.GetFileNameWithoutExtension(outputAssemblyFullPath) + (isPEImage ? ".exe" : ""));

        logger.Information(
            $"Writing AppHost: {Path.GetFileName(outputFullPath)}{(isPEImage ? " (PE format)" : "")}");

        // NOTE: The current .NET Core AppHost implementation does not work properly
        //   without writing this value, which is not cool.
        //   I think the default behavior can be inferred from its own filename.
        if (Utilities.UpdateBytes(
            ms,
            appBinaryPathPlaceholderSearchValue,
            outputAssemblyNameBytes))
        {
            // Why not just have two binary templates and use them differently?
            if (isPEImage && options.CreationOptions?.AssemblyType == AssemblyTypes.WinExe)
            {
                PEUtils.SetWindowsGraphicalUserInterfaceBit(ms);
            }

            using (var fs = new FileStream(
                outputFullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                ms.CopyTo(fs);
                fs.Flush();
            }

            if (!CommonUtilities.IsInWindows)
            {
                while (true)
                {
                    var r = Utilities.chmod(outputFullPath,
                        chmodFlags.S_IXOTH | chmodFlags.S_IROTH |
                        chmodFlags.S_IXGRP | chmodFlags.S_IRGRP |
                        chmodFlags.S_IXUSR | chmodFlags.S_IWUSR | chmodFlags.S_IRUSR);
                    if (r != -1)
                    {
                        break;
                    }
                    var errno = Marshal.GetLastWin32Error();
                    if (errno != Utilities.EINTR)
                    {
                        Marshal.ThrowExceptionForHR(errno);
                    }
                }
            }
        }
        else
        {
            logger.Error(
                $"Invalid AppHost template file: {appHostTemplateFullPath}");
        }
    }
}
