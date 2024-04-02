/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Collections;
using System.Collections.Generic;
using chibicc.toolchain.Logging;

namespace chibild.Internal;

internal sealed class TypeDefinitionCache : IEnumerable<TypeDefinition>
{
    private readonly ILogger logger;
    private readonly List<TypeDefinition> cached = new();
    private IEnumerator<TypeDefinition>? source;

    public TypeDefinitionCache(
        ILogger logger,
        IEnumerable<TypeDefinition> source)
    {
        this.logger = logger;
        this.source = source.GetEnumerator();
    }

    public IEnumerator<TypeDefinition> GetEnumerator()
    {
        var index = 0;
        var hit = 0;
        var miss = 0;

        while (true)
        {
            if (index < this.cached.Count)
            {
                hit++;
                var type = this.cached[index++];
                yield return type;
            }
            else if (this.source != null)
            {
                miss++;
                if (this.source.MoveNext())
                {
                    var type = this.source.Current;
                    this.cached.Add(type);
                    index++;
                    yield return type;
                }
                else
                {
                    this.source.Dispose();
                    this.source = null;
                    break;
                }
            }
            else
            {
                break;
            }
        }

        this.logger.Trace($"Stat: TypeDefinitionCache: Hit={hit}, Miss={miss}, Ratio={((double)hit / (hit + miss)):F2}");
    }

    IEnumerator IEnumerable.GetEnumerator() =>
        this.GetEnumerator();
}
