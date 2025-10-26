namespace ebuild.api
{
    [Flags]
    /// <summary>
    /// Describes the kind of binary produced by a module.
    /// The enum is marked with <see cref="FlagsAttribute"/> to allow combination in rare
    /// scenarios where multiple behaviors are required; most uses treat values as a single choice.
    /// </summary>
    public enum ModuleType
    {
        /// <summary>
        /// Produces a static library (archive of object files).
        /// </summary>
        StaticLibrary,

        /// <summary>
        /// Produces a shared (dynamic) library.
        /// </summary>
        SharedLibrary,

        /// <summary>
        /// Produces a native executable for the target platform.
        /// </summary>
        Executable,

        /// <summary>
        /// Produces a Win32 GUI/console executable (Windows-specific semantics).
        /// </summary>
        ExecutableWin32,

        /// <summary>
        /// Represents a helper module that can't be built directly; typically used for
        /// code generation, as a container for shared settings or loading a prebuilt library (with public libraries etc.).
        /// </summary>
        LibraryLoader,
    }
}