using ebuild.api.Compiler;
using ebuild.api.Linker;

namespace ebuild.api.Toolchain
{


    public interface IToolchain
    {
        string Name { get; }


        ICompilerFactory? GetCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams);
        ILinkerFactory? GetLinkerFactory(ModuleBase module, IModuleInstancingParams instancingParams);
        ICompilerFactory? GetResourceCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams) => null;



        Task<CompilerBase> CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            var factory = GetCompilerFactory(module, instancingParams) ?? throw new Exception($"No compiler factory found for module '{module.Name}' with toolchain '{Name}'");
            if (!factory.CanCreate(module, instancingParams))
                throw new Exception($"Compiler factory '{factory.Name}' cannot create compiler for module '{module.Name}' with toolchain '{Name}'");
            var compiler = factory.CreateCompiler(module, instancingParams);
            return Task.FromResult(compiler);
        }

        Task<LinkerBase> CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            var factory = GetLinkerFactory(module, instancingParams) ?? throw new Exception($"No linker factory found for module '{module.Name}' with toolchain '{Name}'");
            if (!factory.CanCreate(module, instancingParams))
                throw new Exception($"Linker factory '{factory.Name}' cannot create linker for module '{module.Name}' with toolchain '{Name}'");
            var linker = factory.CreateLinker(module, instancingParams);
            return Task.FromResult(linker);
        }

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