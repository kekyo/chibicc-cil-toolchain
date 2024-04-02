/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using chibicc.toolchain.Tokenizing;

namespace chibicc.toolchain.Archiving;

public sealed class SymbolComparer : IEqualityComparer<Symbol>
{
    private SymbolComparer()
    {
    }
    
    public bool Equals(Symbol x, Symbol y) =>
        x.Directive.Text.Equals(y.Directive.Text) &&
        x.Name.Text.Equals(y.Name.Text);

    public int GetHashCode(Symbol obj)
    {
        unchecked
        {
            return
                (obj.Directive.Text.GetHashCode() * 397) ^
                obj.Name.Text.GetHashCode();
        }
    }

    public static readonly SymbolComparer Instance = new();
}
