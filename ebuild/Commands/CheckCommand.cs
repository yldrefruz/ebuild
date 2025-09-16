using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using ebuild.Modules.BuildGraph;


namespace ebuild.Commands
{
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
            var buildGraph = await file.BuildOrGetBuildGraph(ModuleInstancingParams) ?? throw new CommandException(string.Format("Failed to get build graph for {0}", file.GetFilePath()));
            
            if (buildGraph.HasCircularDependency())
            {
                var errorMsg = string.Format("Circular dependency detected in {0}\n{1}", file.GetFilePath(), buildGraph.GetCircularDependencyPathString());
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
            var buildGraph = await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams);
            if (buildGraph == null)
            {
                console.Error.WriteLine("Failed to get build graph for {0}", moduleFile.GetFilePath());
                return;
            }

            console.Output.WriteLine("Dependencies for {0}", moduleFile.GetFilePath());
            console.Output.WriteLine("\n{0}", GetDependencyGraphString(buildGraph.GetRootNode()));
        }

        private static string GetDependencyGraphString(Node node, int depth = 0)
        {
            return GetDependencyGraphString(node, depth, new HashSet<Node>());
        }

        private static string GetDependencyGraphString(Node node, int depth, HashSet<Node> visited)
        {
            var result = new System.Text.StringBuilder();
            var indent = new string(' ', depth * 2);
            
            if (node is ModuleDeclarationNode moduleNode)
            {
                // Check if this node creates a circular dependency
                var isCircular = visited.Contains(node);
                var nodeName = isCircular ? $"{moduleNode.Module.Name} (circular dependency)" : moduleNode.Module.Name;
                result.AppendLine($"{indent}{nodeName}");
                
                // If circular, don't traverse further to avoid infinite recursion
                if (isCircular)
                {
                    return result.ToString();
                }
                
                visited.Add(node);
                
                // Get module dependencies (other ModuleDeclarationNode children)
                var moduleDependencies = node.Children.Joined().OfType<ModuleDeclarationNode>();
                foreach (var child in moduleDependencies)
                {
                    result.Append($"{indent}|-");
                    result.Append(GetDependencyGraphString(child, depth + 1, visited));
                }
                
                visited.Remove(node);
            }
            else
            {
                result.AppendLine($"{indent}{node.Name}");
            }

            return result.ToString();
        }
    }
}