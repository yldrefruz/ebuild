using System.Runtime.InteropServices;
using ebuild.api.Toolchain;
using ebuild.cli;
using ebuild.Platforms;

namespace ebuild.Commands
{
    public abstract class ModuleCreatingCommand : Command
    {       
        [Argument(0, Name="module-file", Description = "the module file to build", IsRequired = true)]
        public string ModuleFile = ".";
        [Option("configuration", ShortName = "c", Description = "the build configuration to use")]
        public string Configuration = Config.Get().DefaultBuildConfiguration;
        [Option("toolchain", Description = "the toolchain to use")]
        public string Toolchain = IToolchainRegistry.Get().GetDefaultToolchainName() ?? "";
        [Option("additional-compiler-option", ShortName = "C", Description = "additional compiler options to pass into compiler")]
        public string[] AdditionalCompilerOptions = [];
        [Option("additional-linker-option", ShortName = "L", Description = "additional linker options to pass into linker")]
        public string[] AdditionalLinkerOptions = [];
        [Option("additional-dependency-path", ShortName = "P", Description = "additional paths to search dependency modules at")]
        public string[] AdditionalDependencyPaths = [];
        [Option("target-architecture", ShortName = "t", Description = "the target architecture to use")]
        public Architecture TargetArchitecture = RuntimeInformation.OSArchitecture;
        [Option("platform", ShortName = "m", Description = "the target platform to use")]
        public string Platform = PlatformRegistry.GetHostPlatform().Name;
        [Option("option", ShortName = "D", Description = "the options to pass into module")]
        public Dictionary<string, string> Options = [];

        public ModuleInstancingParams ModuleInstancingParams => _moduleInstancingParamsCache ??= new()
        {
            AdditionalCompilerOptions = [.. AdditionalCompilerOptions],
            AdditionalLinkerOptions = [.. AdditionalLinkerOptions],
            AdditionalDependencyPaths = [.. AdditionalDependencyPaths],
            Architecture = TargetArchitecture,
            Toolchain = IToolchainRegistry.Get().GetToolchain(Toolchain) ?? throw new Exception($"Toolchain '{Toolchain}' not found"),
            Configuration = Configuration,
            Options = Options,
            Platform = PlatformRegistry.GetInstance().Get(Platform),
            SelfModuleReference = new api.ModuleReference(ModuleFile)
        };
        private ModuleInstancingParams? _moduleInstancingParamsCache;
    }
}