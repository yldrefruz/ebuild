using System.Runtime.InteropServices;

namespace ebuild.api.Linker
{



    public class LinkerSettings
    {
        /// <summary>
        /// The list of input object/library file paths to feed into the linker.
        /// </summary>
        public required string[] InputFiles;

        /// <summary>
        /// Path for the linker output artifact (library or executable).
        /// </summary>
        public required string OutputFile;

        /// <summary>
        /// Requested output type (static/shared library, executable, etc.).
        /// </summary>
        public required ModuleType OutputType;

        /// <summary>
        /// Target architecture for the linker invocation (x86/x64/ARM, etc.).
        /// </summary>
        public required Architecture TargetArchitecture;

        /// <summary>
        /// Directory used for intermediate build artifacts produced during linking.
        /// </summary>
        public required string IntermediateDir;

        /// <summary>
        /// Additional library search paths to pass to the linker.
        /// </summary>
        public string[] LibraryPaths = [];

        /// <summary>
        /// Raw linker flags to pass to the linker executable.
        /// </summary>
        public string[] LinkerFlags = [];

        /// <summary>
        /// Whether the linker should produce separate debug symbol files when supported.
        /// </summary>
        public bool ShouldCreateDebugFiles = false;

        /// <summary>
        /// Indicates whether the current link is part of a debug build configuration.
        /// </summary>
        public bool IsDebugBuild = false;

        /// <summary>
        /// Libraries that should be delay-loaded by the runtime (Windows-specific behavior).
        /// </summary>
        public string[] DelayLoadLibraries = [];
    
    }
}