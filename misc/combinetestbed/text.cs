/////////////////////////////////////////////////////////////////////////////////////
//
// chibias-cil - The specialized backend CIL assembler for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

namespace C.type
{
    public struct Foo
    {
        public int a;
        public short b;
        public byte c;
    }
}

namespace C
{
    public static class text
    {
        public static long foo_calc(C.type.Foo foo)
        {
            return foo.a + foo.b * 2 + foo.c * 3;
        }
    }
}
