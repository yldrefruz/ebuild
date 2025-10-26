namespace ebuild.api
{
    /// <summary>
    /// Represents supported C++ language standard levels used by the toolchain factories
    /// and compilers in the build system. Values are ordered roughly by chronology.
    /// </summary>
    public enum CppStandards
    {
        /// <summary>
        /// ISO C++ 98 / C++03 compatibility level.
        /// </summary>
        Cpp98,

        /// <summary>
        /// ISO C++ 03 compatibility level.
        /// </summary>
        Cpp03,

        /// <summary>
        /// ISO C++ 11 (modern C++ features: auto, range-for, lambdas, etc.).
        /// </summary>
        Cpp11,

        /// <summary>
        /// ISO C++ 14 (small language and library improvements over C++11).
        /// </summary>
        Cpp14,

        /// <summary>
        /// ISO C++ 17 (structured bindings, if-init, and library additions).
        /// </summary>
        Cpp17,

        /// <summary>
        /// ISO C++ 20 (concepts, ranges, coroutines and additional library features).
        /// </summary>
        Cpp20,

        /// <summary>
        /// ISO C++ 23 (ongoing standardisation additions beyond C++20).
        /// </summary>
        Cpp23,

        /// <summary>
        /// Alias for selecting the latest available C++ standard supported by the toolchain.
        /// </summary>
        CppLatest,
    }

    /// <summary>
    /// Represents supported C language standard levels used by compilers in the build system.
    /// </summary>
    public enum CStandards
    {
        /// <summary>
        /// ANSI C89 / C90 compatibility level.
        /// </summary>
        C89,

        /// <summary>
        /// ISO C99 standard.
        /// </summary>
        C99,

        /// <summary>
        /// ISO C11 standard (atomic, _Generic, improved threading support).
        /// </summary>
        C11,

        /// <summary>
        /// ISO C17 standard (bug fixes and clarifications to C11).
        /// </summary>
        C17,

        /// <summary>
        /// Placeholder for the upcoming C2x standard (post-C17 standards work).
        /// </summary>
        C2x,
    }
}