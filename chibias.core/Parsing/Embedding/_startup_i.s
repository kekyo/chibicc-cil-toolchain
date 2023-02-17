.function _module_ int32 _startup args:string[]
	.local int8** ; argv
	.local int32  ; index

	ldarg.0
	ldlen
	conv.i4
	ldc.i4.1
	add
	conv.u
	sizeof int8*
	mul.ovf.un
	localloc
	stloc.0
	ldloc.0
	call System.Reflection.Assembly.GetEntryAssembly
	callvirt System.Reflection.Assembly.get_Location
	call System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi string
	stind.i
	ldc.i4.0
	stloc.1
	br.s ENTRY

LOOP:
	ldloc.0
	ldloc.1
	ldc.i4.1
	add
	conv.i
	sizeof int8*
	mul
	add
	ldarg.0
	ldloc.1
	ldelem.ref
	call System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi string
	stind.i
	ldloc.1
	ldc.i4.1
	add
	stloc.1

ENTRY:
	ldloc.1
	ldarg.0
	ldlen
	conv.i4
	blt.s LOOP

	ldarg.0
	ldlen
	conv.i4
	ldc.i4.1
	add
	ldloc.0
	call main
	ret
