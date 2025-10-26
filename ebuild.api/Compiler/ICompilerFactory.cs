namespace ebuild.api.Compiler
{


    public interface ICompilerFactory
    {
        /// <summary>
        /// Human-readable unique name identifying the compiler factory (for example "msvc" or "gcc").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The concrete <see cref="CompilerBase"/> type created by this factory.
        /// </summary>
        Type CompilerType { get; }

        /// <summary>
        /// Create a compiler instance suitable for building the specified <paramref name="module"/>.
        /// </summary>
        /// <param name="module">The module for which the compiler will be created.</param>
        /// <param name="instancingParams">Context and parameters used when instancing the module.</param>
        /// <returns>A new <see cref="CompilerBase"/> instance configured for the module.</returns>
        CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams);

        /// <summary>
        /// Returns whether this factory can create a suitable compiler for the given module and
        /// instancing parameters. Use this to detect whether a toolchain is compatible with a
        /// particular module configuration.
        /// </summary>
        /// <param name="module">The module to evaluate.</param>
        /// <param name="instancingParams">Instantiation context for the module.</param>
        /// <returns><c>true</c> when the factory can create a compiler for the module; otherwise <c>false</c>.</returns>
        bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams);

        /// <summary>
        /// Returns the filesystem path to the compiler executable used by this factory for the
        /// specified module and instancing parameters.
        /// </summary>
        /// <param name="module">The module being compiled.</param>
        /// <param name="instancingParams">Instantiation context for the module.</param>
        /// <returns>Filesystem path to the compiler executable; may be platform-specific.</returns>
        string GetExecutablePath(ModuleBase module, IModuleInstancingParams instancingParams);
    }
}