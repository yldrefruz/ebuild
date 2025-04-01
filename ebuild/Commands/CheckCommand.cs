using System.CommandLine;
using ebuild.Compilers;
using Microsoft.Extensions.Logging;

namespace ebuild.Commands;

public class CheckCommand
{
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("Check");

    private enum CheckTypes
    {
        CircularDependency,
        PrintDependencies
    }

    private readonly Command _command = new("check", "check the module health and relevant info");

    private readonly Argument<CheckTypes> _type = new("type", "the type of the check");


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
                    await CheckCircularDependency(
                        ModuleInstancingParams.FromOptionsAndArguments(context));
                    break;
                case CheckTypes.PrintDependencies:
                    var compilerInstancingParams =
                        ModuleInstancingParams.FromOptionsAndArguments(context);
                    await PrintDependencies(compilerInstancingParams);
                    break;
            }
        });
    }

    private async Task CheckCircularDependency(ModuleInstancingParams moduleInstancingParams)
    {
        var file = (ModuleFile)moduleInstancingParams.SelfModuleReference;
        var tree = await file.GetDependencyTree(moduleInstancingParams);
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


    private async Task PrintDependencies(ModuleInstancingParams moduleInstancingParams)
    {
        var moduleFile = (ModuleFile)moduleInstancingParams.SelfModuleReference;
        var depTree = await moduleFile.GetDependencyTree(moduleInstancingParams);
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