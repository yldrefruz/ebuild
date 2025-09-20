using ebuild.api;
using ebuild.api.Compiler;
using ebuild.api.Linker;
using ebuild.api.Toolchain;
using ebuild.Compilers;
using ebuild.Linkers;

namespace ebuild.Toolchains
{

    public class MSVCToolchain : IToolchain
    {
        public string Name => "msvc";

        public ICompilerFactory? GetCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new MsvcClCompilerFactory();
        }

        public ICompilerFactory? GetResourceCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new MsvcRcCompilerFactory();
        }

        public ILinkerFactory? GetLinkerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            if (module.Type == ModuleType.StaticLibrary)
            {
                return new MsvcLibLinkerFactory();
            }
            return new MsvcLinkLinkerFactory();
        }
    }
}