using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;


namespace ebuild.Commands;

[Command("check", Description = "check the module health and relevant info")]
public class CheckCommand : ModuleCreatingCommand
{
    public override ValueTask ExecuteAsync(IConsole console)
    {
        throw new CommandException("Please specify a check type.");
    }
}


[Command("check circular-dependencies", Description = "check the module for circular dependencies")]
public class CheckCircularDependencyCommand : CheckCommand
{
    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var file = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
        var tree = await file.BuildOrGetDependencyTree(ModuleInstancingParams) ?? throw new CommandException(string.Format("Failed to get dependency tree for {0}", file.GetFilePath()));
        if (tree.HasCircularDependency())
        {
            var errorMsg = string.Format("Circular dependency detected in {0}\n{1}", file.GetFilePath(), tree.GetCircularDependencyGraphString());
            throw new CommandException(errorMsg);
        }
        else
        {
            console.Output.WriteLine("No circular dependency detected in {0}", file.GetFilePath());
        }
    }
}


[Command("check print-dependencies", Description = "print the module dependencies")]
public class CheckPrintDependenciesCommand : CheckCommand
{
    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
        var depTree = await moduleFile.BuildOrGetDependencyTree(ModuleInstancingParams);
        if (depTree == null)
        {
            console.Error.WriteLine("Failed to get dependency tree for {0}", moduleFile.GetFilePath());
            return;
        }

        console.Output.WriteLine("Dependencies for {0}", moduleFile.GetFilePath());
        console.Output.WriteLine("\n{0}", depTree.ToString());
    }
}