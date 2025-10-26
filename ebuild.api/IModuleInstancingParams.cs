using System.Runtime.InteropServices;
using ebuild.api.Toolchain;

namespace ebuild.api
{
    public interface IModuleInstancingParams
    {
        /// <summary>
        /// Creates a copy of these instancing parameters targeted at <paramref name="targetModuleReference"/>.
        /// Implementations (see <c>ModuleInstancingParams</c>) typically copy all fields but replace
        /// <see cref="SelfModuleReference"/> with the provided <paramref name="targetModuleReference"/>.
        /// </summary>
        /// <param name="targetModuleReference">The module reference that will become the <see cref="SelfModuleReference"/> of the returned copy.</param>
        /// <returns>A new <see cref="IModuleInstancingParams"/> instance with the same settings but targeting <paramref name="targetModuleReference"/>.</returns>
        public IModuleInstancingParams CreateCopyFor(ModuleReference targetModuleReference);

        /// <summary>
        /// Reference identifying the module being instantiated. This contains the file path,
        /// output type, version and any module-specific options. Concrete implementations may
        /// require this property to be populated (see <c>ModuleInstancingParams.SelfModuleReference</c> which is required).
        /// </summary>
        public ModuleReference SelfModuleReference { get; }

        /// <summary>
        /// Build configuration name (for example: "Debug" or "Release").
        /// Implementations often provide a reasonable default; <c>ModuleInstancingParams</c> defaults
        /// this to <c>Config.Get().DefaultBuildConfiguration</c> when not explicitly set.
        /// </summary>
        public string Configuration { get; }

        /// <summary>
        /// Toolchain to use for compilation and linking. Implementations typically default this to
        /// the system default toolchain (<c>IToolchainRegistry.Get().GetDefaultToolchain()</c>) when not provided.
        /// </summary>
        public IToolchain Toolchain { get; }

        /// <summary>
        /// Target CPU architecture for the module (examples: x64, Arm64). Defaults to the host
        /// architecture (see <c>RuntimeInformation.OSArchitecture</c>) in concrete implementations.
        /// </summary>
        public Architecture Architecture { get; }

        /// <summary>
        /// Target platform abstraction used by the build and toolchain layers. Concrete implementations
        /// commonly default this to the host platform (see <c>PlatformRegistry.GetHostPlatform()</c>).
        /// </summary>
        public PlatformBase Platform { get; }

        /// <summary>
        /// Arbitrary key/value options that may influence module instancing and generation behavior.
        /// Implementations typically initialize this to an empty dictionary when not supplied.
        /// </summary>
        public Dictionary<string, string> Options { get; }

        /// <summary>
        /// Additional compiler flags to append when invoking the compiler for this module.
        /// Implementations typically default this to an empty list when not supplied.
        /// </summary>
        public List<string> AdditionalCompilerOptions { get; }

        /// <summary>
        /// Additional linker flags to append when invoking the linker for this module.
        /// Implementations typically default this to an empty list when not supplied.
        /// </summary>
        public List<string> AdditionalLinkerOptions { get; }

        /// <summary>
        /// Additional filesystem paths that should be searched for link-time dependencies.
        /// Implementations typically default this to an empty list when not supplied.
        /// </summary>
        public List<string> AdditionalDependencyPaths { get; }

        /// <summary>
        /// Convert these instancing parameters to a <see cref="ModuleContext"/>. The concrete
        /// <c>ModuleInstancingParams.ToModuleContext()</c> implementation sets fields such as
        /// <c>AdditionalDependencyPaths</c>, <c>Configuration</c>, <c>Options</c>, initializes
        /// <c>Messages</c> to an empty list, and sets <c>TargetArchitecture</c> to <see cref="Architecture"/>.
        /// </summary>
        /// <returns>A new <see cref="ModuleContext"/> populated using the values from this instance.</returns>
        public ModuleContext ToModuleContext();
    }
}