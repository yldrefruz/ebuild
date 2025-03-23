using System.CommandLine;
using System.Threading.Tasks;
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
                    await CheckCircularDependency(CompilerRegistry.CompilerInstancingParams.FromOptionsAndArguments(context));
                    break;
                case CheckTypes.PrintDependencies:
                    var compilerInstancingParams = CompilerRegistry.CompilerInstancingParams.FromOptionsAndArguments(context);
                    await PrintDependencies(compilerInstancingParams);
                    break;
            }
        });
    }

    private async Task CheckCircularDependency(CompilerRegistry.CompilerInstancingParams compilerInstancingParams)
    {
        ModuleFile file = ModuleFile.Get(compilerInstancingParams.ModuleFile);
        DependencyTree? tree = await file.GetDependencyTree(compilerInstancingParams.Configuration, PlatformRegistry.GetHostPlatform(),
            compilerInstancingParams.CompilerName);
        if (tree == null)
        {
            Logger.LogError("Failed to get dependency tree for {file}", file.FilePath);
            return;
        }
        if (tree.HasCircularDependency())
        {
            Logger.LogError("Circular dependency detected in {file}", file.FilePath);
            Logger.LogError("\n{circular_dependency_graph}", tree.GetCircularDependencyGraphString());
        }
        else
        {
            Logger.LogInformation("No circular dependency detected in {file}", file.FilePath);
        }
    }


    private async Task PrintDependencies(CompilerRegistry.CompilerInstancingParams compilerInstancingParams)
    {
        var moduleFile = ModuleFile.Get(compilerInstancingParams.ModuleFile);
        var depTree = await moduleFile.GetDependencyTree(compilerInstancingParams.Configuration, PlatformRegistry.GetHostPlatform(),
            compilerInstancingParams.CompilerName);
        if (depTree == null)
        {
            Logger.LogError("Failed to get dependency tree for {file}", moduleFile.FilePath);
            return;
        }
        Logger.LogInformation("Dependencies for {file}", moduleFile.FilePath);
        Logger.LogInformation("\n{dependencies}", depTree.ToString());
    }

    public static implicit operator Command(CheckCommand c) => c._command;
}