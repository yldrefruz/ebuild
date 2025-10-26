namespace ebuild.api
{
    /// <summary>
    /// Represents the optimization level requested when compiling a module.
    /// The enum is used by the toolchain implementations to map to platform-specific
    /// compiler flags (GCC/Clang/MSVC equivalents are shown in the comments).
    /// </summary>
    public enum OptimizationLevel
    {
        /// <summary>
        /// No optimization. Example flags: <c>-O0</c> (GCC/Clang), <c>/Od</c> (MSVC).
        /// </summary>
        None,

        /// <summary>
        /// Optimize for binary size. Example flags: <c>-Os</c> (GCC/Clang), <c>/O1</c> (MSVC).
        /// </summary>
        Size,

        /// <summary>
        /// Optimize for execution speed. Example flags: <c>-O2</c> (GCC/Clang), <c>/O2</c> (MSVC).
        /// </summary>
        Speed,

        /// <summary>
        /// Maximum optimization aggressiveness. Example flags: <c>-O3</c> (GCC/Clang), <c>/Ox</c> (MSVC).
        /// Use with care as it may increase code size or change floating-point semantics.
        /// </summary>
        Max
    }
}