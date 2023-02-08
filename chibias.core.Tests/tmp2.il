.assembly tmp2
{
}
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )
  .ver 4:0:0:0
}
.class public auto ansi abstract sealed beforefieldinit C.data
  extends [mscorlib]System.Object
{
  .field public static int32 gvalue
}
.class public auto ansi abstract sealed C.text
  extends [mscorlib]System.Object
{
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
