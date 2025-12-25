using ebuild.Modules.BuildGraph;
using ebuild.cli;

namespace ebuild.Commands
{
    [Command("check circular-dependencies", Description = "check the module for circular dependencies")]
    public class CheckCircularDependencyCommand : ModuleCreatingCommand
    {
        public override async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(cancellationToken);
            var file = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var buildGraph = await file.BuildOrGetBuildGraph(ModuleInstancingParams) ?? throw new Exception(string.Format("Failed to get build graph for {0}", file.GetFilePath()));

            if (buildGraph.HasCircularDependency())
            {
                var errorMsg = string.Format("Circular dependency detected in {0}\n{1}", file.GetFilePath(), buildGraph.GetCircularDependencyPathString());
                throw new Exception(errorMsg);
            }
            else
            {
                Console.WriteLine("No circular dependency detected in {0}", file.GetFilePath());
            }
            return 0;
        }
    }


    [Command("check print-dependencies", Description = "print the module dependencies")]
    public class CheckPrintDependenciesCommand : ModuleCreatingCommand
    {
        public override async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var buildGraph = await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams);
            if (buildGraph == null)
            {
                throw new Exception(string.Format("Failed to get build graph for {0}", moduleFile.GetFilePath()));
            }
            Console.WriteLine("Dependencies for {0}", moduleFile.GetFilePath());
            Console.WriteLine("\n{0}", GetDependencyGraphString(buildGraph.GetRootNode()));
            return 0;
        }

        private static string GetDependencyGraphString(Node node, int depth = 0)
        {
            return GetDependencyGraphString(node, depth, []);
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