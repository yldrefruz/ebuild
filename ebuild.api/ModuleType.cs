namespace ebuild.api
{
    [Flags]
    public enum ModuleType
    {
        StaticLibrary,
        SharedLibrary,
        Executable,
        ExecutableWin32,
        LibraryLoader,
    }
}