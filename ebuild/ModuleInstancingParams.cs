using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.Compilers;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild;

public class ModuleInstancingParams(ModuleReference moduleFileReference) : IModuleInstancingParams
{
    public IModuleInstancingParams CreateCopyFor(ModuleReference targetModuleReference)
    {
        return new ModuleInstancingParams(targetModuleReference)
        {
            Configuration = Configuration,
            CompilerName = CompilerName,
            Architecture = Architecture,
            PlatformName = PlatformName,
            Options = Options,
            Logger = Logger,
            AdditionalCompilerOptions = AdditionalCompilerOptions,
            AdditionalLinkerOptions = AdditionalLinkerOptions,
            AdditionalDependencyPaths = AdditionalDependencyPaths
        };
    }

    public readonly ModuleReference SelfModuleReference = moduleFileReference;
    public ModuleReference GetSelfModuleReference() => SelfModuleReference;
    public string Configuration = Config.Get().DefaultBuildConfiguration;
    public string GetConfiguration() => Configuration;
    public string CompilerName = PlatformRegistry.GetHostPlatform().GetDefaultCompilerName() ?? "Null";
    public string GetCompilerName() => CompilerName;
    public Architecture Architecture = RuntimeInformation.OSArchitecture;
    public Architecture GetArchitecture() => Architecture;
    public string PlatformName = PlatformRegistry.GetHostPlatform().GetName();
    public string GetPlatformName() => PlatformName;
    public Dictionary<string, string>? Options;
    public Dictionary<string, string>? GetOptions() => Options;
    public ILogger? Logger;
    public List<string>? AdditionalCompilerOptions;
    public List<string>? GetAdditionalCompilerOptions() => AdditionalCompilerOptions;
    public List<string>? AdditionalLinkerOptions;
    public List<string>? GetAdditionalLinkerOptions() => AdditionalLinkerOptions;
    public List<string>? AdditionalDependencyPaths;
    public List<string>? GetAdditionalDependencyPaths() => AdditionalDependencyPaths;

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

    public ModuleContext ToModuleContext() => new(reference: SelfModuleReference,
        platform: PlatformName, compiler: CompilerName)
    {
        AdditionalDependencyPaths = AdditionalDependencyPaths ?? [],
        Configuration = Configuration,
        Options = Options ?? [],
        Messages = [],
        TargetArchitecture = Architecture,
        InstancingParams = this,
    };



    public static explicit operator ModuleContext(ModuleInstancingParams p) => p.ToModuleContext();
}