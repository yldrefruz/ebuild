using System.CommandLine;

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

    public static void AddCompilerCreationParams(this Command command)
    {
        command.AddArgument(ModuleArgument);
        command.AddOption(ConfigurationOption);
        command.AddOption(CompilerName);
        command.AddOption(AdditionalCompilerOptions);
        command.AddOption(AdditionalLinkerOptions);
    }
}