using System.Runtime.InteropServices;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ebuild.api.Toolchain;
using ebuild.Compilers;
using ebuild.Platforms;

namespace ebuild.Commands;






public abstract class ModuleCreatingCommand : ICommand
{
    [CommandParameter(0, Description = "the module file to build")]
    public required string ModuleFile { get; init; }
    [CommandOption("configuration", 'c', Description = "the build configuration to use")]
    public string Configuration { get; init; } = Config.Get().DefaultBuildConfiguration;
    [CommandOption("toolchain", Description = "the toolchain to use")]
    public string Toolchain { get; init; } = IToolchainRegistry.Get().GetDefaultToolchainName() ?? "";
    [CommandOption("additional-compiler-option", 'C', Description = "additional compiler options to pass into compiler")]
    public string[] AdditionalCompilerOptions { get; init; } = [];
    [CommandOption("additional-linker-option", 'L', Description = "additional linker options to pass into linker")]
    public string[] AdditionalLinkerOptions { get; init; } = [];
    [CommandOption("additional-dependency-path", 'P', Description = "additional paths to search dependency modules at")]
    public string[] AdditionalDependencyPaths { get; init; } = [];
    [CommandOption("target-architecture", 't', Description = "the target architecture to use")]
    public Architecture TargetArchitecture { get; init; } = RuntimeInformation.OSArchitecture;
    public string Platform { get; init; } = PlatformRegistry.GetHostPlatform().Name;
    [CommandOption("option", 'D', Description = "the options to pass into module")]
    public Dictionary<string, string> Options { get; init; } = [];

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
    public abstract ValueTask ExecuteAsync(IConsole console);
}