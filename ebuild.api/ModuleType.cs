﻿namespace ebuild.api;

[Flags]
public enum ModuleType
{
    StaticLibrary = 1 << 0,
    SharedLibrary = 1 << 1,
    Executable = 1 << 2,
    ExecutableWin32 = 1 << 3
}