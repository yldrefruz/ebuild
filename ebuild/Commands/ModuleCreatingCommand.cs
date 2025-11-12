using System.Runtime.InteropServices;
using CliFx.Attributes;
using CliFx.Extensibility;
using CliFx.Infrastructure;
using ebuild.api.Toolchain;
using ebuild.Platforms;

namespace ebuild.Commands
{
    public struct OptionsArray()
    {
        public Dictionary<string, string> Options { get; set; } = [];


        public static implicit operator Dictionary<string, string>(OptionsArray optionsArray) => optionsArray.Options;
        public static implicit operator OptionsArray(Dictionary<string, string> dictionary) => new() { Options = dictionary };
    }
    public class OptionsArrayConverter : CliFx.Extensibility.BindingConverter<OptionsArray>
    {
        public override OptionsArray Convert(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return new OptionsArray();
            var entries = value.Split(';');
            var dictionary = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                var parts = entry.Split('=', 2);
                if (parts.Length != 2)
                    throw new FormatException("Invalid dictionary entry format. Expected format: key=value");
                dictionary[parts[0]] = parts[1];
            }
            return dictionary;
        }
    }

    public abstract class ModuleCreatingCommand : BaseCommand
    {
        [CommandParameter(0, Description = "the module file to build", IsRequired = false)]
        public string ModuleFile { get; init; } = ".";
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
        [CommandOption("option", 'D', Description = "the options to pass into module", Converter = typeof(OptionsArrayConverter))]
        public OptionsArray Options { get; init; } = new OptionsArray();

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
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
        }
    }
}