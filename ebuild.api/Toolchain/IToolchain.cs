using ebuild.api.Compiler;
using ebuild.api.Linker;

namespace ebuild.api.Toolchain
{


    public interface IToolchain
    {
        string Name { get; }


        ICompilerFactory? GetCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams);
        ILinkerFactory? GetLinkerFactory(ModuleBase module, IModuleInstancingParams instancingParams);





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
    }
}