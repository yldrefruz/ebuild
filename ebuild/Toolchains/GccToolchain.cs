using ebuild.api;
using ebuild.api.Compiler;
using ebuild.api.Linker;
using ebuild.api.Toolchain;
using ebuild.Compilers;
using ebuild.Linkers;

namespace ebuild.Toolchains
{
    public class GccToolchain : IToolchain
    {
        public string Name => "gcc";

        public ICompilerFactory? GetCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new GccCompilerFactory();
        }

        public ILinkerFactory? GetLinkerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            if (module.Type == ModuleType.StaticLibrary)
            {
                return new ArLinkerFactory();
            }
            return new GccLinkerFactory();
        }
    }
}