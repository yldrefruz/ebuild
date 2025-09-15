using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Compilers
{
    public class GccCompilerFactory : ICompilerFactory
    {
        public string Name => "gcc";

        public Type CompilerType => typeof(GccCompiler);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            // GCC is available on Unix-like platforms (Linux, macOS, etc.)
            return instancingParams.Platform.Name != "windows";
        }

        public CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new GccCompiler(instancingParams.Architecture);
        }
    }
}