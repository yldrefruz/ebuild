using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.Compilers;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild;

public class ModuleInstancingParams(ModuleReference moduleFileReference)
{
    public readonly ModuleReference SelfModuleReference = moduleFileReference;
    public string Configuration = Config.Get().DefaultBuildConfiguration;
    public string CompilerName = PlatformRegistry.GetHostPlatform().GetDefaultCompilerName() ?? "Null";
    public Architecture Architecture = RuntimeInformation.OSArchitecture;
    public string PlatformName = PlatformRegistry.GetHostPlatform().GetName();
    public Dictionary<string, string>? Options;
    public ILogger? Logger;
    public List<string>? AdditionalCompilerOptions;
    public List<string>? AdditionalLinkerOptions;
    public List<string>? AdditionalDependencyPaths;

    /// <summary>
    /// Create a CompilerInstancingParams from the command line arguments and options.
    /// The logger will be null if created this way
    /// </summary>
    /// <param name="context">the context to use for creation</param>
    /// <returns></returns>
    public static ModuleInstancingParams FromOptionsAndArguments(InvocationContext context)
    {
        return new ModuleInstancingParams(
            context.ParseResult.GetValueForArgument(CompilerCreationUtilities.ModuleArgument))
        {
            Configuration = context.ParseResult.GetValueForOption(CompilerCreationUtilities.ConfigurationOption) ??
                            "Null",
            Logger = null,
            CompilerName = context.ParseResult.GetValueForOption(CompilerCreationUtilities.CompilerName) ??
                           CompilerRegistry.GetDefaultCompilerName(),
            AdditionalCompilerOptions =
                context.ParseResult.GetValueForOption(CompilerCreationUtilities.AdditionalCompilerOptions),
            AdditionalLinkerOptions =
                context.ParseResult.GetValueForOption(CompilerCreationUtilities.AdditionalLinkerOptions),
            AdditionalDependencyPaths =
                context.ParseResult.GetValueForOption(CompilerCreationUtilities.AdditionalDependencyPaths),
            Architecture = context.ParseResult.GetValueForOption(CompilerCreationUtilities.Architecture),
            PlatformName = context.ParseResult.GetValueForOption(CompilerCreationUtilities.Platform)!,
            Options = context.ParseResult.GetValueForOption(CompilerCreationUtilities.Options)
        };
    }


    public static explicit operator ModuleContext(ModuleInstancingParams p) => new()
    {
        AdditionalDependencyPaths = p.AdditionalDependencyPaths ?? new List<string>(),
        Compiler = p.CompilerName,
        Platform = p.PlatformName,
        SelfReference = p.SelfModuleReference,
        Configuration = p.Configuration,
        Options = p.Options ?? new Dictionary<string, string>(),
        Messages = new List<ModuleContext.Message>(),
        TargetArchitecture = p.Architecture
    };
}