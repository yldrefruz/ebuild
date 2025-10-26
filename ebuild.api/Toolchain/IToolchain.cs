using ebuild.api.Compiler;
using ebuild.api.Linker;

namespace ebuild.api.Toolchain
{


    public interface IToolchain
    {
        /// <summary>
        /// Unique name identifying the toolchain (for example "msvc" or "gcc").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns a compiler factory appropriate for the given module and instancing
        /// parameters, or <c>null</c> when the toolchain does not provide a compiler for
        /// the module.
        /// </summary>
        /// <param name="module">The module for which a compiler factory is requested.</param>
        /// <param name="instancingParams">Context used when instancing the module.</param>
        /// <returns>An <see cref="ICompilerFactory"/> or <c>null</c> if unavailable.</returns>
        ICompilerFactory? GetCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams);

        /// <summary>
        /// Returns a linker factory appropriate for the given module and instancing
        /// parameters, or <c>null</c> when the toolchain does not provide a linker for
        /// the module.
        /// </summary>
        /// <param name="module">The module for which a linker factory is requested.</param>
        /// <param name="instancingParams">Context used when instancing the module.</param>
        /// <returns>An <see cref="ILinkerFactory"/> or <c>null</c> if unavailable.</returns>
        ILinkerFactory? GetLinkerFactory(ModuleBase module, IModuleInstancingParams instancingParams);

        /// <summary>
        /// Returns a factory for resource compilers (for example .rc resource compilers), or
        /// <c>null</c> if the toolchain does not provide one. Default implementation returns <c>null</c>.
        /// </summary>
        /// <param name="module">The module for which a resource compiler factory is requested.</param>
        /// <param name="instancingParams">Context used when instancing the module.</param>
        /// <returns>An <see cref="ICompilerFactory"/> for resource compilation, or <c>null</c>.</returns>
        ICompilerFactory? GetResourceCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams) => null;


        /// <summary>
        /// Create a compiler for the specified module using the toolchain. This default
        /// implementation locates a compiler factory via <see cref="GetCompilerFactory"/>
        /// and delegates creation; it throws when no suitable factory exists.
        /// </summary>
        /// <param name="module">Module to compile.</param>
        /// <param name="instancingParams">Module instancing parameters.</param>
        /// <returns>A task completing with a configured <see cref="CompilerBase"/> instance.</returns>
        /// <exception cref="Exception">Thrown when no compiler factory is available or the
        /// factory reports it cannot create the compiler.</exception>
        Task<CompilerBase> CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            var factory = GetCompilerFactory(module, instancingParams) ?? throw new Exception($"No compiler factory found for module '{module.Name}' with toolchain '{Name}'");
            if (!factory.CanCreate(module, instancingParams))
                throw new Exception($"Compiler factory '{factory.Name}' cannot create compiler for module '{module.Name}' with toolchain '{Name}'");
            var compiler = factory.CreateCompiler(module, instancingParams);
            return Task.FromResult(compiler);
        }

        /// <summary>
        /// Create a linker for the specified module using the toolchain. Default
        /// implementation locates a linker factory and delegates creation; it throws when
        /// no suitable factory exists.
        /// </summary>
        /// <param name="module">Module to link.</param>
        /// <param name="instancingParams">Module instancing parameters.</param>
        /// <returns>A task completing with a configured <see cref="LinkerBase"/> instance.</returns>
        /// <exception cref="Exception">Thrown when no linker factory is available or the
        /// factory reports it cannot create the linker.</exception>
        Task<LinkerBase> CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            var factory = GetLinkerFactory(module, instancingParams) ?? throw new Exception($"No linker factory found for module '{module.Name}' with toolchain '{Name}'");
            if (!factory.CanCreate(module, instancingParams))
                throw new Exception($"Linker factory '{factory.Name}' cannot create linker for module '{module.Name}' with toolchain '{Name}'");
            var linker = factory.CreateLinker(module, instancingParams);
            return Task.FromResult(linker);
        }

        /// <summary>
        /// Create a resource compiler for the specified module, if the toolchain exposes a
        /// resource compiler factory. Returns <c>null</c> when no resource compiler is
        /// available.
        /// </summary>
        /// <param name="module">Module to compile resources for.</param>
        /// <param name="instancingParams">Module instancing parameters.</param>
        /// <returns>A task completing with a <see cref="CompilerBase"/> instance or <c>null</c>.</returns>
        /// <exception cref="Exception">Thrown when a resource compiler factory exists but
        /// reports it cannot create a compiler for the module.</exception>
        Task<CompilerBase?> CreateResourceCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            var factory = GetResourceCompilerFactory(module, instancingParams);
            if (factory == null)
                return Task.FromResult<CompilerBase?>(null);
            if (!factory.CanCreate(module, instancingParams))
                throw new Exception($"Resource compiler factory '{factory.Name}' cannot create resource compiler for module '{module.Name}' with toolchain '{Name}'");
            var compiler = factory.CreateCompiler(module, instancingParams);
            return Task.FromResult<CompilerBase?>(compiler);
        }
    }
}