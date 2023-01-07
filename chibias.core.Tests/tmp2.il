.assembly tmp2
{
}
.class public auto ansi abstract sealed beforefieldinit C.module
  extends [mscorlib]System.Object
{
  .field public static int32 gvalue

  .method public hidebysig static int32 ret3() cil managed
  {
    ldc.i4.3
    ret
  }
  .method public hidebysig static int32 ret5() cil managed
  {
    ldc.i4.5
    ret
  }
  .method public hidebysig static int32 add2(int32 x, int32 y) cil managed
  {
    ldarg.0
    ldarg.1
    add
    ret
  }
  .method public hidebysig static int32 sub2(int32 x, int32 y) cil managed
  {
    ldarg.0
    ldarg.1
    sub
    ret
  }
  .method public hidebysig static int32 add6(int32 a, int32 b, int32 c, int32 d, int32 e, int32 f) cil managed
  {
    ldarg.0
    ldarg.1
    ldarg.2
    ldarg.3
    ldarg.s 4
    ldarg.s 5
    add
    add
    add
    add
    add
    ret
  }
}
