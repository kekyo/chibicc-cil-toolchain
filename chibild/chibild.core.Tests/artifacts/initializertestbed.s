; ../../chibild/bin/Debug/net6.0/cil-chibild -mnet45 -shared -o initializertestbed.dll initializertestbed.s -L/home/kouji/.nuget/packages/microsoft.netframework.referenceassemblies.net45/1.0.3/build/.NETFramework/v4.5 -lmscorlib

.global public int pvar;
.initializer internal
    ldc.i4.s 123
    stsfld pvar
    ret
.global file int fvar;
.initializer file
    ldc.i4.s 45
    stsfld fvar
    ret

