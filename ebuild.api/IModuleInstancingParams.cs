using System.Runtime.InteropServices;
using ebuild.api.Toolchain;

namespace ebuild.api
{
    public interface IModuleInstancingParams
    {
        public IModuleInstancingParams CreateCopyFor(ModuleReference targetModuleReference);
        public ModuleReference SelfModuleReference { get; }
        public string Configuration { get; }
        public IToolchain Toolchain { get; }
        public Architecture Architecture { get; }
        public PlatformBase Platform { get; }
        public Dictionary<string, string> Options { get; }
        public List<string> AdditionalCompilerOptions { get; }
        public List<string> AdditionalLinkerOptions { get; }
        public List<string> AdditionalDependencyPaths { get; }

        public ModuleContext ToModuleContext();
    }
}