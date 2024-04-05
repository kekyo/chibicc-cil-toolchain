.function _module_ int32() _start
    .local string[] args
    .local string path
	.local int8** argv
	.local bool V_3
	.local string extension
	.local bool V_5
	.local int32 index
	.local uint8[] argBytes
	.local uint8[] argBytes0
	.local System.Runtime.InteropServices.GCHandle argHandle
	.local bool V_10

    call string[]() System.Environment.GetCommandLineArgs
    stloc args
    
    ldloc args
    ldc.i4.0
    ldelem.ref
    stloc path
    
    ldloc path
    ldstr ".dll"
    callvirt bool(string) System.String.EndsWith
    stloc.3
    
    ldloc.3
    brfalse.s L1
    
    call System.OperatingSystem() System.Environment.get_OSVersion
    callvirt System.PlatformID() System.OperatingSystem.get_Platform
    ldc.i4.2
    beq.s L2
    ldstr ""
    br.s L3
L2:
    ldstr ".exe"
L3:
    stloc.s extension
    
    ldloc path
    call string(string) System.IO.Path.GetDirectoryName
    dup
    brtrue.s L4
    pop
    ldstr ""
L4:
    ldloc path
    call string(string) System.IO.Path.GetFileNameWithoutExtension
    ldloc.s extension
    call string(string,string) System.String.Concat
    call string(string,string) System.IO.Path.Combine
    stloc path
    
    ldloc path
    call bool(string) System.IO.File.Exists
    stloc.s 5
    
    ldloc.s 5
    brfalse.s L1
    
    ldloc args
    ldc.i4.0
    ldloc path
    stelem.ref
    
L1:
    ldloc args
    ldlen
    conv.i4
    ldc.i4.1
    add
    conv.u
    sizeof int8*
    mul.ovf.un
    localloc
    stloc argv
    
    ldc.i4.0
    stloc.s index
    br.s L6
    
L5:
    call System.Text.Encoding() System.Text.Encoding.get_UTF8
    ldloc args
    ldloc.s index
    ldelem.ref
    callvirt uint8[](string) System.Text.Encoding.GetBytes
    stloc.s argBytes    
    
    ldloc.s argBytes
    ldlen
    conv.i4
    ldc.i4.1
    add
    newarr int8
    stloc.s argBytes0
    
    ldloc.s argBytes
    ldloc.s argBytes0
    ldloc.s argBytes
    ldlen
    conv.i4
    call void(System.Array,System.Array,int32) System.Array.Copy
    
    ldloc.s argBytes0
    ldc.i4.3
    call System.Runtime.InteropServices.GCHandle(object,System.Runtime.InteropServices.GCHandleType) System.Runtime.InteropServices.GCHandle.Alloc
    stloc.s argHandle
    
    ldloc argv
    ldloc.s index
    conv.i
    sizeof int8*
    mul
    add
    ldloca.s argHandle
    call nint() System.Runtime.InteropServices.GCHandle.AddrOfPinnedObject
    stind.i
    
    ldloc.s index
    ldc.i4.1
    add
    stloc.s index
    
L6:
    ldloc.s index
    ldloc args
    ldlen
    conv.i4
    clt
    stloc.s V_10
    
    ldloc.s V_10
    brtrue.s L5
    
    ldloc args
    ldlen
    conv.i4
    ldloc argv
    call main
    ret
