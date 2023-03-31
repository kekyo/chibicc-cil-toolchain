.function _module_ void(args:string[]) _start
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
	call System.Reflection.Assembly() System.Reflection.Assembly.GetEntryAssembly
	callvirt string() System.Reflection.Assembly.get_Location
	call nint(string) System.Runtime.InteropServices.Marshal.StringToCoTaskMemAnsi
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
	call nint(string) System.Runtime.InteropServices.Marshal.StringToCoTaskMemAnsi
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
