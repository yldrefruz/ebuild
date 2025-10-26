namespace ebuild.api.Linker
{


    /// <summary>
    /// Factory interface responsible for producing <see cref="LinkerBase"/> instances for
    /// a specific toolchain. Implementations perform capability checks and create linkers
    /// configured for a particular <see cref="ModuleBase"/> and instancing parameters.
    /// </summary>
    public interface ILinkerFactory
    {
        /// <summary>
        /// Unique human-readable name of the linker factory (for example "msvc" or "gcc").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Concrete <see cref="LinkerBase"/> type produced by this factory.
        /// </summary>
        Type LinkerType { get; }

        /// <summary>
        /// Create a linker instance for the specified module and instancing parameters.
        /// </summary>
        /// <param name="module">Module that will be linked.</param>
        /// <param name="moduleInstancingParams">Contextual parameters used when instancing the module.</param>
        /// <returns>A new <see cref="LinkerBase"/> instance.</returns>
        LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams moduleInstancingParams);

        /// <summary>
        /// Determine whether this factory can create a suitable linker for the given module
        /// and instancing parameters. Use to check compatibility before attempting creation.
        /// </summary>
        /// <param name="module">Module to evaluate.</param>
        /// <param name="instancingParams">Instantiation context for the module.</param>
        /// <returns><c>true</c> when this factory can create a linker for the module; otherwise <c>false</c>.</returns>
        bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams);

    }
}