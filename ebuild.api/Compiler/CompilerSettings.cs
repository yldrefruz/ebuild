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
        public required string SourceFile;
        public required string OutputFile;
        public required Architecture TargetArchitecture;
        public required ModuleType ModuleType;
        public CPUExtensions CPUExtension = CPUExtensions.Default;
        public bool EnableExceptions = false;
        public bool EnableFastFloatingPointOperations = true;
        public bool EnableRTTI = true;
        public bool IsDebugBuild = true;
        public List<Definition> Definitions = [];
        public List<string> IncludePaths = [];
        public List<string> ForceIncludes = [];
        public bool EnableDebugFileCreation = true;
        public required CppStandards CppStandard;
        public CStandards? CStandard = null;
        public OptimizationLevel Optimization = OptimizationLevel.Speed;
        public List<string> OtherFlags = [];
    }
}