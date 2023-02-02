/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System.Collections;
using System.Collections.Generic;

namespace chibias.Internal;

internal sealed class TypeDefinitionCache : IEnumerable<TypeDefinition>
{
    private readonly List<TypeDefinition> cached = new();
    private IEnumerator<TypeDefinition>? source;

    public TypeDefinitionCache(IEnumerable<TypeDefinition> source) =>
        this.source = source.GetEnumerator();

    public IEnumerator<TypeDefinition> GetEnumerator()
    {
        var index = 0;
        while (true)
        {
            if (index < this.cached.Count)
            {
                var type = this.cached[index++];
                yield return type;
            }
            else if (this.source != null)
            {
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
    }

    IEnumerator IEnumerable.GetEnumerator() =>
        this.GetEnumerator();
}
