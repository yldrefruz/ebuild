using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Compilers
{
    public class MsvcClCompilerFactory : ICompilerFactory
    {
        public string Name => "msvc.cl";

        public Type CompilerType => typeof(MsvcClCompiler);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return instancingParams.Platform.Name == "windows";
        }
        public CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            var created = new MsvcClCompiler(instancingParams.Architecture);
            return created;
        }
    }
}
