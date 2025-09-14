using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Toolchain;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild;

public class ModuleInstancingParams : IModuleInstancingParams
{
    public IModuleInstancingParams CreateCopyFor(ModuleReference targetModuleReference)
    {
        return new ModuleInstancingParams
        {
            SelfModuleReference = targetModuleReference,
            Configuration = Configuration,
            Toolchain = Toolchain,
            Architecture = Architecture,
            Platform = Platform,
            Options = Options,
            Logger = Logger,
            AdditionalCompilerOptions = AdditionalCompilerOptions,
            AdditionalLinkerOptions = AdditionalLinkerOptions,
            AdditionalDependencyPaths = AdditionalDependencyPaths
        };
    }

    public required ModuleReference SelfModuleReference { get; set; }
    public string Configuration { get; set; } = Config.Get().DefaultBuildConfiguration;
    public IToolchain Toolchain { get; set; } = IToolchainRegistry.Get().GetDefaultToolchain()!;
    public Architecture Architecture { get; set; } = RuntimeInformation.OSArchitecture;
    public PlatformBase Platform { get; set; } = PlatformRegistry.GetHostPlatform();
    public Dictionary<string, string> Options { get; set; } = [];
    public ILogger? Logger;
    public List<string> AdditionalCompilerOptions { get; set; } = [];
    public List<string> AdditionalLinkerOptions { get; set; } = [];
    public List<string> AdditionalDependencyPaths { get; set; } = [];

    public ModuleContext ToModuleContext() => new(reference: SelfModuleReference,
        platform: Platform, toolchain: Toolchain)
    {
        AdditionalDependencyPaths = AdditionalDependencyPaths,
        Configuration = Configuration,
        Options = Options,
        Messages = [],
        TargetArchitecture = Architecture,
        InstancingParams = this,
    };



    public static explicit operator ModuleContext(ModuleInstancingParams p) => p.ToModuleContext();
}