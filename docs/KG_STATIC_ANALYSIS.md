# KG-UP-GOAT.dll static analysis notes

This file documents the static analysis used to clone the CTF patch without reusing KG code.

## Sample

```text
file: KG-UP-GOAT.dll
sha256: d13240737e1c7ea92a9bf9568375fd2b87f659c38d8a3942a71f74cb3919c30b
size: 15360 bytes
format: PE32+ x86-64 DLL
compiler string: GCC / MinGW-w64
```

## Imports of interest

```text
GetModuleHandleA
LoadLibraryA
GetProcAddress
VirtualProtect
VirtualQuery
Sleep
```

## Deobfuscated strings

The DLL stores strings XOR-obfuscated with a byte-incrementing key. Relevant decoded strings:

```text
DisableThreadLibraryCalls
GetModuleFileNameA
kernel32.dll
LoadLibraryA
GetModuleHandleA
VirtualProtect
xgameruntime.dll
KG-UP-GOAT.dll
isTrial
xzNope
```

## Effective behavior

On DLL process attach, KG resolves kernel32 APIs, loads or gets `xgameruntime.dll`,
scans the loaded module image for the ASCII string:

```text
isTrial\0
```

When it finds a match, it calls `VirtualProtect`, writes:

```text
xzNope\0
```

then restores the old protection.

The clean module implements only this CTF-local patch and omits KG network/updater/telemetry behavior.
