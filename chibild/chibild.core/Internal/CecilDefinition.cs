/////////////////////////////////////////////////////////////////////////////////////
//
// chibicc-toolchain - The specialized backend toolchain for chibicc-cil
// Copyright (c) Kouji Matsui(@kozy_kekyo, @kekyo @mastodon.cloud)
//
// Licensed under MIT: https://opensource.org/licenses/MIT
//
/////////////////////////////////////////////////////////////////////////////////////

namespace chibild.Internal;

// https://gist.github.com/kekyo/bc8e411b846c98d2f2ae7b839ef77fea

internal static class CecilDefinition
{
    public static System.Collections.Generic.Dictionary<string, Mono.Cecil.Cil.OpCode> GetOpCodes() =>
        new(System.StringComparer.OrdinalIgnoreCase)
    {
        { "arglist", Mono.Cecil.Cil.OpCodes.Arglist },
        { "ceq", Mono.Cecil.Cil.OpCodes.Ceq },
        { "cgt", Mono.Cecil.Cil.OpCodes.Cgt },
        { "cgt.un", Mono.Cecil.Cil.OpCodes.Cgt_Un },
        { "clt", Mono.Cecil.Cil.OpCodes.Clt },
        { "clt.un", Mono.Cecil.Cil.OpCodes.Clt_Un },
        { "ldftn", Mono.Cecil.Cil.OpCodes.Ldftn },
        { "ldvirtftn", Mono.Cecil.Cil.OpCodes.Ldvirtftn },
        { "ldarg", Mono.Cecil.Cil.OpCodes.Ldarg },
        { "ldarga", Mono.Cecil.Cil.OpCodes.Ldarga },
        { "starg", Mono.Cecil.Cil.OpCodes.Starg },
        { "ldloc", Mono.Cecil.Cil.OpCodes.Ldloc },
        { "ldloca", Mono.Cecil.Cil.OpCodes.Ldloca },
        { "stloc", Mono.Cecil.Cil.OpCodes.Stloc },
        { "localloc", Mono.Cecil.Cil.OpCodes.Localloc },
        { "endfilter", Mono.Cecil.Cil.OpCodes.Endfilter },
        { "unaligned", Mono.Cecil.Cil.OpCodes.Unaligned },
        { "volatile", Mono.Cecil.Cil.OpCodes.Volatile },
        { "tail", Mono.Cecil.Cil.OpCodes.Tail },
        { "initobj", Mono.Cecil.Cil.OpCodes.Initobj },
        { "constrained", Mono.Cecil.Cil.OpCodes.Constrained },
        { "cpblk", Mono.Cecil.Cil.OpCodes.Cpblk },
        { "initblk", Mono.Cecil.Cil.OpCodes.Initblk },
        { "rethrow", Mono.Cecil.Cil.OpCodes.Rethrow },
        { "sizeof", Mono.Cecil.Cil.OpCodes.Sizeof },
        { "refanytype", Mono.Cecil.Cil.OpCodes.Refanytype },
        { "readonly", Mono.Cecil.Cil.OpCodes.Readonly },
        { "nop", Mono.Cecil.Cil.OpCodes.Nop },
        { "break", Mono.Cecil.Cil.OpCodes.Break },
        { "ldarg.0", Mono.Cecil.Cil.OpCodes.Ldarg_0 },
        { "ldarg.1", Mono.Cecil.Cil.OpCodes.Ldarg_1 },
        { "ldarg.2", Mono.Cecil.Cil.OpCodes.Ldarg_2 },
        { "ldarg.3", Mono.Cecil.Cil.OpCodes.Ldarg_3 },
        { "ldloc.0", Mono.Cecil.Cil.OpCodes.Ldloc_0 },
        { "ldloc.1", Mono.Cecil.Cil.OpCodes.Ldloc_1 },
        { "ldloc.2", Mono.Cecil.Cil.OpCodes.Ldloc_2 },
        { "ldloc.3", Mono.Cecil.Cil.OpCodes.Ldloc_3 },
        { "stloc.0", Mono.Cecil.Cil.OpCodes.Stloc_0 },
        { "stloc.1", Mono.Cecil.Cil.OpCodes.Stloc_1 },
        { "stloc.2", Mono.Cecil.Cil.OpCodes.Stloc_2 },
        { "stloc.3", Mono.Cecil.Cil.OpCodes.Stloc_3 },
        { "ldarg.s", Mono.Cecil.Cil.OpCodes.Ldarg_S },
        { "ldarga.s", Mono.Cecil.Cil.OpCodes.Ldarga_S },
        { "starg.s", Mono.Cecil.Cil.OpCodes.Starg_S },
        { "ldloc.s", Mono.Cecil.Cil.OpCodes.Ldloc_S },
        { "ldloca.s", Mono.Cecil.Cil.OpCodes.Ldloca_S },
        { "stloc.s", Mono.Cecil.Cil.OpCodes.Stloc_S },
        { "ldnull", Mono.Cecil.Cil.OpCodes.Ldnull },
        { "ldc.i4.m1", Mono.Cecil.Cil.OpCodes.Ldc_I4_M1 },
        { "ldc.i4.0", Mono.Cecil.Cil.OpCodes.Ldc_I4_0 },
        { "ldc.i4.1", Mono.Cecil.Cil.OpCodes.Ldc_I4_1 },
        { "ldc.i4.2", Mono.Cecil.Cil.OpCodes.Ldc_I4_2 },
        { "ldc.i4.3", Mono.Cecil.Cil.OpCodes.Ldc_I4_3 },
        { "ldc.i4.4", Mono.Cecil.Cil.OpCodes.Ldc_I4_4 },
        { "ldc.i4.5", Mono.Cecil.Cil.OpCodes.Ldc_I4_5 },
        { "ldc.i4.6", Mono.Cecil.Cil.OpCodes.Ldc_I4_6 },
        { "ldc.i4.7", Mono.Cecil.Cil.OpCodes.Ldc_I4_7 },
        { "ldc.i4.8", Mono.Cecil.Cil.OpCodes.Ldc_I4_8 },
        { "ldc.i4.s", Mono.Cecil.Cil.OpCodes.Ldc_I4_S },
        { "ldc.i4", Mono.Cecil.Cil.OpCodes.Ldc_I4 },
        { "ldc.i8", Mono.Cecil.Cil.OpCodes.Ldc_I8 },
        { "ldc.r4", Mono.Cecil.Cil.OpCodes.Ldc_R4 },
        { "ldc.r8", Mono.Cecil.Cil.OpCodes.Ldc_R8 },
        { "dup", Mono.Cecil.Cil.OpCodes.Dup },
        { "pop", Mono.Cecil.Cil.OpCodes.Pop },
        { "jmp", Mono.Cecil.Cil.OpCodes.Jmp },
        { "call", Mono.Cecil.Cil.OpCodes.Call },
        { "calli", Mono.Cecil.Cil.OpCodes.Calli },
        { "ret", Mono.Cecil.Cil.OpCodes.Ret },
        { "br.s", Mono.Cecil.Cil.OpCodes.Br_S },
        { "brfalse.s", Mono.Cecil.Cil.OpCodes.Brfalse_S },
        { "brtrue.s", Mono.Cecil.Cil.OpCodes.Brtrue_S },
        { "beq.s", Mono.Cecil.Cil.OpCodes.Beq_S },
        { "bge.s", Mono.Cecil.Cil.OpCodes.Bge_S },
        { "bgt.s", Mono.Cecil.Cil.OpCodes.Bgt_S },
        { "ble.s", Mono.Cecil.Cil.OpCodes.Ble_S },
        { "blt.s", Mono.Cecil.Cil.OpCodes.Blt_S },
        { "bne.un.s", Mono.Cecil.Cil.OpCodes.Bne_Un_S },
        { "bge.un.s", Mono.Cecil.Cil.OpCodes.Bge_Un_S },
        { "bgt.un.s", Mono.Cecil.Cil.OpCodes.Bgt_Un_S },
        { "ble.un.s", Mono.Cecil.Cil.OpCodes.Ble_Un_S },
        { "blt.un.s", Mono.Cecil.Cil.OpCodes.Blt_Un_S },
        { "br", Mono.Cecil.Cil.OpCodes.Br },
        { "brfalse", Mono.Cecil.Cil.OpCodes.Brfalse },
        { "brtrue", Mono.Cecil.Cil.OpCodes.Brtrue },
        { "beq", Mono.Cecil.Cil.OpCodes.Beq },
        { "bge", Mono.Cecil.Cil.OpCodes.Bge },
        { "bgt", Mono.Cecil.Cil.OpCodes.Bgt },
        { "ble", Mono.Cecil.Cil.OpCodes.Ble },
        { "blt", Mono.Cecil.Cil.OpCodes.Blt },
        { "bne.un", Mono.Cecil.Cil.OpCodes.Bne_Un },
        { "bge.un", Mono.Cecil.Cil.OpCodes.Bge_Un },
        { "bgt.un", Mono.Cecil.Cil.OpCodes.Bgt_Un },
        { "ble.un", Mono.Cecil.Cil.OpCodes.Ble_Un },
        { "blt.un", Mono.Cecil.Cil.OpCodes.Blt_Un },
        { "switch", Mono.Cecil.Cil.OpCodes.Switch },
        { "ldind.i1", Mono.Cecil.Cil.OpCodes.Ldind_I1 },
        { "ldind.u1", Mono.Cecil.Cil.OpCodes.Ldind_U1 },
        { "ldind.i2", Mono.Cecil.Cil.OpCodes.Ldind_I2 },
        { "ldind.u2", Mono.Cecil.Cil.OpCodes.Ldind_U2 },
        { "ldind.i4", Mono.Cecil.Cil.OpCodes.Ldind_I4 },
        { "ldind.u4", Mono.Cecil.Cil.OpCodes.Ldind_U4 },
        { "ldind.i8", Mono.Cecil.Cil.OpCodes.Ldind_I8 },
        { "ldind.i", Mono.Cecil.Cil.OpCodes.Ldind_I },
        { "ldind.r4", Mono.Cecil.Cil.OpCodes.Ldind_R4 },
        { "ldind.r8", Mono.Cecil.Cil.OpCodes.Ldind_R8 },
        { "ldind.ref", Mono.Cecil.Cil.OpCodes.Ldind_Ref },
        { "stind.ref", Mono.Cecil.Cil.OpCodes.Stind_Ref },
        { "stind.i1", Mono.Cecil.Cil.OpCodes.Stind_I1 },
        { "stind.i2", Mono.Cecil.Cil.OpCodes.Stind_I2 },
        { "stind.i4", Mono.Cecil.Cil.OpCodes.Stind_I4 },
        { "stind.i8", Mono.Cecil.Cil.OpCodes.Stind_I8 },
        { "stind.r4", Mono.Cecil.Cil.OpCodes.Stind_R4 },
        { "stind.r8", Mono.Cecil.Cil.OpCodes.Stind_R8 },
        { "add", Mono.Cecil.Cil.OpCodes.Add },
        { "sub", Mono.Cecil.Cil.OpCodes.Sub },
        { "mul", Mono.Cecil.Cil.OpCodes.Mul },
        { "div", Mono.Cecil.Cil.OpCodes.Div },
        { "div.un", Mono.Cecil.Cil.OpCodes.Div_Un },
        { "rem", Mono.Cecil.Cil.OpCodes.Rem },
        { "rem.un", Mono.Cecil.Cil.OpCodes.Rem_Un },
        { "and", Mono.Cecil.Cil.OpCodes.And },
        { "or", Mono.Cecil.Cil.OpCodes.Or },
        { "xor", Mono.Cecil.Cil.OpCodes.Xor },
        { "shl", Mono.Cecil.Cil.OpCodes.Shl },
        { "shr", Mono.Cecil.Cil.OpCodes.Shr },
        { "shr.un", Mono.Cecil.Cil.OpCodes.Shr_Un },
        { "neg", Mono.Cecil.Cil.OpCodes.Neg },
        { "not", Mono.Cecil.Cil.OpCodes.Not },
        { "conv.i1", Mono.Cecil.Cil.OpCodes.Conv_I1 },
        { "conv.i2", Mono.Cecil.Cil.OpCodes.Conv_I2 },
        { "conv.i4", Mono.Cecil.Cil.OpCodes.Conv_I4 },
        { "conv.i8", Mono.Cecil.Cil.OpCodes.Conv_I8 },
        { "conv.r4", Mono.Cecil.Cil.OpCodes.Conv_R4 },
        { "conv.r8", Mono.Cecil.Cil.OpCodes.Conv_R8 },
        { "conv.u4", Mono.Cecil.Cil.OpCodes.Conv_U4 },
        { "conv.u8", Mono.Cecil.Cil.OpCodes.Conv_U8 },
        { "callvirt", Mono.Cecil.Cil.OpCodes.Callvirt },
        { "cpobj", Mono.Cecil.Cil.OpCodes.Cpobj },
        { "ldobj", Mono.Cecil.Cil.OpCodes.Ldobj },
        { "ldstr", Mono.Cecil.Cil.OpCodes.Ldstr },
        { "newobj", Mono.Cecil.Cil.OpCodes.Newobj },
        { "castclass", Mono.Cecil.Cil.OpCodes.Castclass },
        { "isinst", Mono.Cecil.Cil.OpCodes.Isinst },
        { "conv.r.un", Mono.Cecil.Cil.OpCodes.Conv_R_Un },
        { "unbox", Mono.Cecil.Cil.OpCodes.Unbox },
        { "throw", Mono.Cecil.Cil.OpCodes.Throw },
        { "ldfld", Mono.Cecil.Cil.OpCodes.Ldfld },
        { "ldflda", Mono.Cecil.Cil.OpCodes.Ldflda },
        { "stfld", Mono.Cecil.Cil.OpCodes.Stfld },
        { "ldsfld", Mono.Cecil.Cil.OpCodes.Ldsfld },
        { "ldsflda", Mono.Cecil.Cil.OpCodes.Ldsflda },
        { "stsfld", Mono.Cecil.Cil.OpCodes.Stsfld },
        { "stobj", Mono.Cecil.Cil.OpCodes.Stobj },
        { "conv.ovf.i1.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I1_Un },
        { "conv.ovf.i2.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I2_Un },
        { "conv.ovf.i4.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I4_Un },
        { "conv.ovf.i8.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I8_Un },
        { "conv.ovf.u1.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U1_Un },
        { "conv.ovf.u2.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U2_Un },
        { "conv.ovf.u4.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U4_Un },
        { "conv.ovf.u8.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U8_Un },
        { "conv.ovf.i.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I_Un },
        { "conv.ovf.u.un", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U_Un },
        { "box", Mono.Cecil.Cil.OpCodes.Box },
        { "newarr", Mono.Cecil.Cil.OpCodes.Newarr },
        { "ldlen", Mono.Cecil.Cil.OpCodes.Ldlen },
        { "ldelema", Mono.Cecil.Cil.OpCodes.Ldelema },
        { "ldelem.i1", Mono.Cecil.Cil.OpCodes.Ldelem_I1 },
        { "ldelem.u1", Mono.Cecil.Cil.OpCodes.Ldelem_U1 },
        { "ldelem.i2", Mono.Cecil.Cil.OpCodes.Ldelem_I2 },
        { "ldelem.u2", Mono.Cecil.Cil.OpCodes.Ldelem_U2 },
        { "ldelem.i4", Mono.Cecil.Cil.OpCodes.Ldelem_I4 },
        { "ldelem.u4", Mono.Cecil.Cil.OpCodes.Ldelem_U4 },
        { "ldelem.i8", Mono.Cecil.Cil.OpCodes.Ldelem_I8 },
        { "ldelem.i", Mono.Cecil.Cil.OpCodes.Ldelem_I },
        { "ldelem.r4", Mono.Cecil.Cil.OpCodes.Ldelem_R4 },
        { "ldelem.r8", Mono.Cecil.Cil.OpCodes.Ldelem_R8 },
        { "ldelem.ref", Mono.Cecil.Cil.OpCodes.Ldelem_Ref },
        { "stelem.i", Mono.Cecil.Cil.OpCodes.Stelem_I },
        { "stelem.i1", Mono.Cecil.Cil.OpCodes.Stelem_I1 },
        { "stelem.i2", Mono.Cecil.Cil.OpCodes.Stelem_I2 },
        { "stelem.i4", Mono.Cecil.Cil.OpCodes.Stelem_I4 },
        { "stelem.i8", Mono.Cecil.Cil.OpCodes.Stelem_I8 },
        { "stelem.r4", Mono.Cecil.Cil.OpCodes.Stelem_R4 },
        { "stelem.r8", Mono.Cecil.Cil.OpCodes.Stelem_R8 },
        { "stelem.ref", Mono.Cecil.Cil.OpCodes.Stelem_Ref },
        { "ldelem", Mono.Cecil.Cil.OpCodes.Ldelem_Any },
        { "stelem", Mono.Cecil.Cil.OpCodes.Stelem_Any },
        { "unbox.any", Mono.Cecil.Cil.OpCodes.Unbox_Any },
        { "conv.ovf.i1", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I1 },
        { "conv.ovf.u1", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U1 },
        { "conv.ovf.i2", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I2 },
        { "conv.ovf.u2", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U2 },
        { "conv.ovf.i4", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I4 },
        { "conv.ovf.u4", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U4 },
        { "conv.ovf.i8", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I8 },
        { "conv.ovf.u8", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U8 },
        { "refanyval", Mono.Cecil.Cil.OpCodes.Refanyval },
        { "ckfinite", Mono.Cecil.Cil.OpCodes.Ckfinite },
        { "mkrefany", Mono.Cecil.Cil.OpCodes.Mkrefany },
        { "ldtoken", Mono.Cecil.Cil.OpCodes.Ldtoken },
        { "conv.u2", Mono.Cecil.Cil.OpCodes.Conv_U2 },
        { "conv.u1", Mono.Cecil.Cil.OpCodes.Conv_U1 },
        { "conv.i", Mono.Cecil.Cil.OpCodes.Conv_I },
        { "conv.ovf.i", Mono.Cecil.Cil.OpCodes.Conv_Ovf_I },
        { "conv.ovf.u", Mono.Cecil.Cil.OpCodes.Conv_Ovf_U },
        { "add.ovf", Mono.Cecil.Cil.OpCodes.Add_Ovf },
        { "add.ovf.un", Mono.Cecil.Cil.OpCodes.Add_Ovf_Un },
        { "mul.ovf", Mono.Cecil.Cil.OpCodes.Mul_Ovf },
        { "mul.ovf.un", Mono.Cecil.Cil.OpCodes.Mul_Ovf_Un },
        { "sub.ovf", Mono.Cecil.Cil.OpCodes.Sub_Ovf },
        { "sub.ovf.un", Mono.Cecil.Cil.OpCodes.Sub_Ovf_Un },
        { "endfinally", Mono.Cecil.Cil.OpCodes.Endfinally },
        { "leave", Mono.Cecil.Cil.OpCodes.Leave },
        { "leave.s", Mono.Cecil.Cil.OpCodes.Leave_S },
        { "stind.i", Mono.Cecil.Cil.OpCodes.Stind_I },
        { "conv.u", Mono.Cecil.Cil.OpCodes.Conv_U },
    };
}