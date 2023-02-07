/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System;
using System.Globalization;
using System.Linq;

namespace chibias.Internal;

internal enum TargetFrameworkIdentifiers
{
    NETFramework,
    NETStandard,
    NETCoreApp,
    //NETPortable,
}

internal readonly struct TargetFramework
{
    private static readonly char[] versionSeparators = { '.' };
    private static readonly char[] postfixSeparators = { '-' };

    public readonly TargetFrameworkIdentifiers Identifier;
    public readonly Version Version;
    public readonly string? Profile;

    public TargetFramework(
        TargetFrameworkIdentifiers identifier, 
        Version version,
        string? profile = null)
    {
        this.Identifier = identifier;
        this.Version = version;
        this.Profile = profile;
    }

    public TargetRuntime Runtime =>
        this.Identifier switch
        {
            TargetFrameworkIdentifiers.NETFramework =>
                this.Version.Major switch
                {
                    1 => this.Version.Minor == 0 ?
                        TargetRuntime.Net_1_0 : TargetRuntime.Net_1_1,
                    4 => TargetRuntime.Net_4_0,
                    _ => TargetRuntime.Net_2_0,
                },
            _ => TargetRuntime.Net_4_0,
        };

    private static int[] ParseVersion(string versionString) =>
        versionString.Split(versionSeparators, StringSplitOptions.RemoveEmptyEntries).
        Select(vs => int.TryParse(vs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1).
        ToArray();

    public static bool TryParse(
        string targetFrameworkMoniker,
        out TargetFramework targetFramework)
    {
        // This implementation is incomplete when give minor tfms.

        var tfm = targetFrameworkMoniker.ToLowerInvariant();
        if (tfm.StartsWith("netstandard"))
        {
            var versions = ParseVersion(
                tfm.Substring("netstandard".Length));
            if (versions.Length == 2 &&
                versions[0] >= 1 && versions[0] <= 2 &&
                ((versions[0] == 1 && versions[1] <= 6) ||
                 (versions[0] == 2 && versions[1] <= 1)))
            {
                targetFramework = new(TargetFrameworkIdentifiers.NETStandard, new(versions[0], versions[1]));
                return true;
            }
        }
        else if (tfm.StartsWith("netcoreapp"))
        {
            var versions = ParseVersion(
                tfm.Substring("netcoreapp".Length));
            if (versions.Length == 2 &&
                versions[0] >= 1 &&
                ((versions[0] == 1 && versions[1] <= 1) ||
                 (versions[0] == 2 && versions[1] <= 2) ||
                 (versions[0] == 3 && versions[1] <= 1)))
            {
                targetFramework = new(TargetFrameworkIdentifiers.NETCoreApp, new(versions[0], versions[1]));
                return true;
            }
        }
        else if (tfm.StartsWith("net"))
        {
            var elements = tfm.
                Split(postfixSeparators, StringSplitOptions.RemoveEmptyEntries);
            var postfix = elements.ElementAtOrDefault(1);

            var versions = ParseVersion(
                elements[0].Substring("net".Length));

            if (versions.Length == 1)
            {
                var intVersion = versions[0];
                if (intVersion < 100)
                {
                    // 20 --> 200, 35 --> 350
                    intVersion *= 10;
                }

                var major = intVersion / 100;
                var minor = (intVersion - major * 100) / 10;
                var build = (intVersion - major * 100 - minor * 10) is { } b && b >= 1 ?
                    (int?)b : null;

                var version = build is { } ?
                    new Version(major, minor, build.Value) :
                    new Version(major, minor);

                if (postfix == "client")
                {
                    targetFramework = new(TargetFrameworkIdentifiers.NETFramework, version, "Client");
                }
                else
                {
                    targetFramework = new(TargetFrameworkIdentifiers.NETFramework, version);
                }
                return true;
            }
            else if (versions.Length == 2 &&
                ((versions[0] == 5 && versions[1] == 0) ||
                 (versions[0] == 6 && versions[1] == 0) ||
                 (versions[0] == 7 && versions[1] == 0) ||
                  versions[0] >= 8))  // Ignores future versions
            {
                targetFramework = new(TargetFrameworkIdentifiers.NETCoreApp, new(versions[0], versions[1]));
                return true;
            }
        }

        targetFramework = default;
        return false;
    }

    public override string ToString() =>
        this.Profile is { } profile ?
            $".{this.Identifier},Version=v{this.Version},Profile={profile}" :
            $".{this.Identifier},Version=v{this.Version}";
}
