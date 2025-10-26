using System.Diagnostics.CodeAnalysis;

namespace ebuild.api
{
    /// <summary>
    /// Base class for platform-specific behavior and file-format conventions.
    ///
    /// Implementations should provide file extension conventions (static/shared libraries,
    /// executables, compiled-object files, resource files) and can supply platform-specific
    /// compiler/linker flags, definitions, include paths and library search paths for a
    /// given <see cref="ModuleBase"/>.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public abstract class PlatformBase(string name)
    {
        /// <summary>
        /// The canonical name of the platform. Typically used for lookups and diagnostics.
        /// </summary>
        public readonly string Name = name;

        /// <summary>
        /// Returns the default toolchain name used on this platform, or <c>null</c> if there is
        /// no single default. Examples: "msvc", "gcc".
        /// </summary>
        /// <returns>The default toolchain name, or <c>null</c> when unspecified.</returns>
        public abstract string? GetDefaultToolchainName();




    /// <summary>
    /// File extension used for compiled object/source output from a compiler for this
    /// platform (for example ".obj" on Windows/MSVC or ".o" on Unix/GCC).
    /// </summary>
    public virtual string ExtensionForCompiledSourceFile => ".obj";

    /// <summary>
    /// Whether this platform produces separate debug symbol files (for example PDB files on
    /// Windows). If <c>false</c> debug information is embedded into the binary or handled
    /// differently by the toolchain.
    /// </summary>
    public virtual bool SupportsDebugFiles => true;

    /// <summary>
    /// File extension used for separate debug symbol files for compiled source (for example
    /// ".pdb" on Windows). Only meaningful when <see cref="SupportsDebugFiles"/> is
    /// <c>true</c>.
    /// </summary>
    public virtual string ExtensionForCompiledSourceFile_DebugFile => ".pdb";

    /// <summary>
    /// File extension for static library outputs on this platform (for example ".lib" or
    /// ".a"). Implementations must provide a platform-specific value.
    /// </summary>
    public abstract string ExtensionForStaticLibrary { get; }

    /// <summary>
    /// File extension for shared library outputs on this platform (for example ".dll",
    /// ".so" or ".dylib"). Implementations must provide a platform-specific value.
    /// </summary>
    public abstract string ExtensionForSharedLibrary { get; }

    /// <summary>
    /// File extension for executable outputs on this platform (for example ".exe" on
    /// Windows or no extension on many Unix systems). Implementations must provide a
    /// platform-specific value.
    /// </summary>
    public abstract string ExtensionForExecutable { get; }

    /// <summary>
    /// File extension used for platform resource source files (for example ".rc" on
    /// Windows). Override if the platform uses a different resource source file format.
    /// </summary>
    public virtual string ExtensionForResourceSourceFile => ".rc";

    /// <summary>
    /// File extension for compiled resource files produced from resource source files
    /// (for example ".res" on Windows).
    /// </summary>
    public virtual string ExtensionForCompiledResourceFile => ".res";

        /// <summary>
        /// Returns platform-specific include paths for the given <paramref name="module"/>.
        /// These paths are appended to the module's include list when compiling for this
        /// platform.
        /// </summary>
        /// <param name="module">The module being compiled for the platform.</param>
        /// <returns>An enumeration of include directory paths. Empty by default.</returns>
        public virtual IEnumerable<string> GetPlatformIncludes(ModuleBase module)
        {
            yield break;
        }
        /// <summary>
        /// Returns platform-specific library search paths used by the linker for the given
        /// <paramref name="module"/>.
        /// </summary>
        /// <param name="module">The module being linked for the platform.</param>
        /// <returns>An enumeration of directory paths to search for libraries. Empty by default.</returns>
        public virtual IEnumerable<string> GetPlatformLibrarySearchPaths(ModuleBase module)
        {
            yield break;
        }
        /// <summary>
        /// Returns platform-specific libraries that should be linked into the given
        /// <paramref name="module"/>. These are library filenames or linker arguments
        /// appropriate for the platform (for example system libraries).
        /// </summary>
        /// <param name="module">The module being linked for the platform.</param>
        /// <returns>An enumeration of libraries or linker arguments. Empty by default.</returns>
        public virtual IEnumerable<string> GetPlatformLibraries(ModuleBase module)
        {
            yield break;
        }
        /// <summary>
        /// Returns linker flags or arguments required on this platform for the given
        /// <paramref name="module"/> (for example -Wl,--start-group on some unix linkers or
        /// /DEBUG on MSVC linkers).
        /// </summary>
        /// <param name="module">The module being linked for the platform.</param>
        /// <returns>An enumeration of linker flags. Empty by default.</returns>
        public virtual IEnumerable<string> GetPlatformLinkerFlags(ModuleBase module)
        {
            yield break;
        }
        /// <summary>
        /// Returns additional compiler flags required on this platform for the given
        /// <paramref name="module"/> (for example position-independent-code flags or
        /// platform-specific warning levels).
        /// </summary>
        /// <param name="module">The module being compiled for the platform.</param>
        /// <returns>An enumeration of compiler flags. Empty by default.</returns>
        public virtual IEnumerable<string> GetPlatformCompilerFlags(ModuleBase module)
        {
            yield break;
        }
        /// <summary>
        /// Returns preprocessor definitions that should be applied when compiling the given
        /// <paramref name="module"/> for this platform. Definitions are returned as
        /// <see cref="Definition"/> instances to capture name/value pairs or conditional
        /// behavior.
        /// </summary>
        /// <param name="module">The module being compiled for the platform.</param>
        /// <returns>An enumeration of platform-specific preprocessor definitions. Empty by default.</returns>
        public virtual IEnumerable<Definition> GetPlatformDefinitions(ModuleBase module)
        {
            yield break;
        }
    }
}