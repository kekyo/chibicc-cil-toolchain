using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using chibicc.toolchain.Logging;
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Parsing;

internal sealed class TokensIterator : IDisposable
{
    private IEnumerator<Token[]>? enumerator;
    private Token[]? stack;

    public TokensIterator(IEnumerable<Token[]> tokens) =>
        this.enumerator = tokens.GetEnumerator();

    public void Dispose()
    {
        this.enumerator?.Dispose();
        this.enumerator = null;
        this.stack = null;
    }

    public bool TryGetNext(out Token[] tokens)
    {
        if (this.stack is { } stack)
        {
            this.stack = null;
            tokens = stack;
            return true;
        }

        while (this.enumerator!.MoveNext())
        {
            if (this.enumerator.Current!.Length >= 1)
            {
                tokens = this.enumerator.Current!;                
                return true;
            }
        }
        
        tokens = null!;
        return false;
    }

    public void PushBack(Token[] tokens)
    {
        Debug.Assert(this.stack == null);
        this.stack = tokens;
    }
}
