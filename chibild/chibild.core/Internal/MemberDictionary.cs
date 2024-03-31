/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace chibild.Internal;

internal sealed class MemberDictionary<TMember>
    where TMember : MemberReference
{
    private readonly ILogger logger;
    private readonly Dictionary<string, TMember> cached = new();
    private readonly Func<TMember, string> getName;
    private IEnumerator<TMember>? source;
    private int hit;
    private int miss;

    public MemberDictionary(
        ILogger logger,
        IEnumerable<TMember> source) :
        this(logger, source, m => m.Name)
    {
    }

    public MemberDictionary(
        ILogger logger,
        IEnumerable<TMember> source,
        Func<TMember, string> getName)
    {
        this.logger = logger;
        this.getName = getName;
        this.source = source.GetEnumerator();
    }

    public bool TryGetMember(string name, out TMember member) =>
        this.TryGetMember<TMember>(name, out member);

    public bool TryGetMember<T>(string name, out T member)
        where T : TMember
    {
        if (this.cached.TryGetValue(name, out var m))
        {
            this.hit++;
            if (m is T tm)
            {
                member = tm;
                return true;
            }
            else
            {
                member = default!;
                return false;
            }
        }

        this.miss++;

        if (this.source == null)
        {
            member = default!;
            return false;
        }

        while (this.source.MoveNext())
        {
            m = this.source.Current;
            var mn = getName(m);

#if NETCOREAPP || NETSTANDRD2_1
            this.cached.TryAdd(mn, m);
#else
            if (!this.cached.ContainsKey(mn))
            {
                this.cached.Add(mn, m);
            }
#endif
            if (mn == name)
            {
                if (m is T tm)
                {
                    member = tm;
                    return true;
                }
                else
                {
                    member = default!;
                    return false;
                }
            }
        }

        this.source.Dispose();
        this.source = null;

        member = default!;
        return false;
    }

    public void Finish()
    {
        this.logger.Trace($"Stat: MemberDictionary<{typeof(TMember).Name}>: Hit={this.hit}, Miss={this.miss}, Ratio={((double)this.hit / (this.hit + this.miss)):F2}");
    }
}
