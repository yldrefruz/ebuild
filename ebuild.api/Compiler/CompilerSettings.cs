using System.Runtime.InteropServices;

namespace ebuild.api.Compiler
{

    /// <summary>
    /// Represents the settings used by the compiler, including source and output files, language standards,
    /// optimization level, and various compilation flags.
    /// </summary>
    /// <remarks>
    /// This class contains configuration options for compiling source code, such as definitions, include paths,
    /// forced includes, language standards for C and C++, optimization preferences, and additional flags.
    /// </remarks>
    public class CompilerSettings
    {
        /// <summary>
        /// Path to the primary source file to be compiled.
        /// </summary>
        public required string SourceFile;

        /// <summary>
        /// Path where the compiler should write its primary output (for example an object file).
        /// </summary>
        public required string OutputFile;

        /// <summary>
        /// Target CPU architecture for the compilation (for example x86, x64, arm).
        /// </summary>
        public required Architecture TargetArchitecture;

        /// <summary>
        /// The module type being built (static library, shared library, executable, etc.).
        /// </summary>
        public required ModuleType ModuleType;

        /// <summary>
        /// Directory used to place intermediate build artifacts (object files, temporary files).
        /// </summary>
        public required string IntermediateDir;

        /// <summary>
        /// CPU extension level to target when emitting code (SSE/AVX/ARM variants).
        /// </summary>
        public CPUExtensions CPUExtension = CPUExtensions.Default;

        /// <summary>
        /// Whether C++ exceptions should be enabled for the compilation unit.
        /// </summary>
        public bool EnableExceptions = false;

        /// <summary>
        /// Whether to enable faster (potentially less accurate) floating-point operations.
        /// </summary>
        public bool EnableFastFloatingPointOperations = true;

        /// <summary>
        /// Whether Run-Time Type Information (RTTI) is enabled.
        /// </summary>
        public bool EnableRTTI = true;

        /// <summary>
        /// Indicates whether the current build configuration is a debug build.
        /// </summary>
        public bool IsDebugBuild = true;

        /// <summary>
        /// Preprocessor definitions to apply when compiling the source.
        /// </summary>
        public List<Definition> Definitions = [];

        /// <summary>
        /// Include directory paths to pass to the compiler.
        /// </summary>
        public List<string> IncludePaths = [];

        /// <summary>
        /// Header files to force-include for every translation unit.
        /// </summary>
        public List<string> ForceIncludes = [];

        /// <summary>
        /// Whether to request creation of a separate debug symbol file where supported.
        /// </summary>
        public bool EnableDebugFileCreation = true;

        /// <summary>
        /// C++ language standard to target (for example C++11, C++17).
        /// </summary>
        public required CppStandards CppStandard;

        /// <summary>
        /// Optional C language standard to target when compiling C sources.
        /// </summary>
        public CStandards? CStandard = null;

        /// <summary>
        /// Optimization level to use during compilation (for example Speed, Size, None).
        /// </summary>
        public OptimizationLevel Optimization = OptimizationLevel.Speed;

        /// <summary>
        /// Any additional raw flags to pass to the compiler.
        /// </summary>
        public List<string> OtherFlags = [];

    }
}