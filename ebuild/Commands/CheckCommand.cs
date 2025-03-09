using System.CommandLine;
using ebuild.api;
using ebuild.Compilers;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild.Commands;

public class CheckCommand
{
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("Check");

    public enum CheckTypes
    {
        CircularDependency,
        PrintDependencies
    }

    private Command _command = new("check", "check the module health and relevant info");

    private Argument<CheckTypes> _type = new("type", "the type of the check");


    public CheckCommand()
    {
        _command.AddArgument(_type);
        _command.AddCompilerCreationParams();

        _command.SetHandler(async (context) =>
        {
            var type = context.ParseResult.GetValueForArgument(_type);
            switch (type)
            {
                default:
                case CheckTypes.CircularDependency:
                    CheckCircularDependency(CompilerRegistry.CompilerInstancingParams.FromOptionsAndArguments(context));
                    break;
                case CheckTypes.PrintDependencies:
                    var compilerInstancingParams =
                        CompilerRegistry.CompilerInstancingParams.FromOptionsAndArguments(context);
                    await PrintDependencies(compilerInstancingParams, new HashSet<string>());
                    break;
            }
        });
    }

    private void CheckCircularDependency(CompilerRegistry.CompilerInstancingParams compilerInstancingParams)
    {
        ModuleFile file = ModuleFile.Get(compilerInstancingParams.ModuleFile);
        //TODO: Circular dependency
    }


    private async Task PrintDependencies(CompilerRegistry.CompilerInstancingParams compilerInstancingParams,
        HashSet<string> dependentModules)
    {
        var moduleFile = ModuleFile.Get(compilerInstancingParams.ModuleFile);
        var moduleContext = new ModuleContext(new FileInfo(compilerInstancingParams.ModuleFile),
            compilerInstancingParams.Configuration, PlatformRegistry.GetHostPlatform(),
            compilerInstancingParams.CompilerName, null);
        var module = await moduleFile.CreateModuleInstance(moduleContext);
        if (module == null)
            return;
        if (dependentModules.Contains(moduleFile.FilePath))
        {
            return;
        }

        Logger.Log(Config.Get().CheckCommandLogLevel, "module {name} file: {path}", module.Name, moduleFile.FilePath);
        var dependencies = await moduleFile.GetDependencies(compilerInstancingParams.Configuration,
            PlatformRegistry.GetHostPlatform(), compilerInstancingParams.CompilerName);
        foreach (var cip in dependencies.Select(dependency => new CompilerRegistry.CompilerInstancingParams(
                     Path.GetFullPath(dependency.FilePath,
                         moduleFile.FilePath))
                 {
                     Configuration = compilerInstancingParams.Configuration,
                     CompilerName = compilerInstancingParams.CompilerName,
                     AdditionalCompilerOptions = compilerInstancingParams.AdditionalCompilerOptions,
                     AdditionalLinkerOptions = compilerInstancingParams.AdditionalLinkerOptions,
                     Logger = compilerInstancingParams.Logger
                 }))
        {
            await PrintDependencies(cip, dependentModules);
        }
    }

    public static implicit operator Command(CheckCommand c) => c._command;
}