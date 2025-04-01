using System.CommandLine;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.Platforms;

namespace ebuild.Compilers;

public static class CompilerCreationUtilities
{
    public static readonly Argument<string> ModuleArgument = new("module file", "module file to use");

    public static readonly Option<string> ConfigurationOption =
        new(new[] { "--configuration", "-c" }, () => Config.Get().DefaultBuildConfiguration,
            "configuration to use for compiling. Default is from Config");

    public static readonly Option<string> CompilerName = new("--compiler", CompilerRegistry.GetDefaultCompilerName,
        "Compiler to use. This is for overriding, will load from config by default. If empty will use the platforms preferred compiler.");

    public static readonly Option<List<string>> AdditionalCompilerOptions =
        new(new[] { "--additional-compiler-option", "-aco" }, "Additional compiler options to pass into compiler")
            { AllowMultipleArgumentsPerToken = true };

    public static readonly Option<List<string>> AdditionalLinkerOptions =
        new(new[] { "--additional-linker-option", "-alo" }, "Additional linker options to pass into linker")
            { AllowMultipleArgumentsPerToken = true };

    public static readonly Option<List<string>> AdditionalDependencyPaths =
        new(new[] { "--additional-dependency-paths", "-adp" }, "Additional paths to search dependency modules at")
            { AllowMultipleArgumentsPerToken = true };

    public static readonly Option<Architecture> Architecture = new(new[] { "--target-architecture", "-ta" },
        () => RuntimeInformation.OSArchitecture, "Target architecture, if available"
    );

    public static readonly Option<string> Platform = new(aliases: new[] { "--platform", "-p" },
        getDefaultValue: () => PlatformRegistry.GetHostPlatform().GetName(),
        description: "The platform to use when compiling"
    );

    public static readonly Option<Dictionary<string, string>> Options = new(aliases: new[] { "--options", "-op" },
        parseArgument: r => r.Tokens.Select(v => v.Value.Split("=")).ToDictionary(k => k[0], k => k[1]),
        description: "The options to pass into module"
    )
    {
        AllowMultipleArgumentsPerToken = true
    };

    public static void AddCompilerCreationParams(this Command command)
    {
        Options.SetDefaultValue(new Dictionary<string, string>());
        command.AddArgument(ModuleArgument);
        command.AddOption(ConfigurationOption);
        command.AddOption(CompilerName);
        command.AddOption(AdditionalCompilerOptions);
        command.AddOption(AdditionalLinkerOptions);
        command.AddOption(AdditionalDependencyPaths);
        command.AddOption(Architecture);
        command.AddOption(Platform);
        command.AddOption(Options);
    }
}