using ebuild.api;

namespace ebuild.Modules.BuildGraph;


public class Graph(ModuleBase module)
{
    public ModuleBase Module { get; init; } = module;
    private Node RootNode = new ModuleDeclarationNode(module);
    
    // Cache for circular dependency detection results
    private bool? _hasCircularDependencyCache = null;
    private List<Node>? _circularDependencyPathCache = null;

    public Node GetRootNode() => RootNode;
    public Worker CreateWorker() => new(this);

    /// <summary>
    /// Checks if the build graph has any circular dependencies
    /// </summary>
    /// <returns>True if circular dependencies exist, false otherwise</returns>
    public bool HasCircularDependency()
    {
        if (_hasCircularDependencyCache.HasValue)
        {
            return _hasCircularDependencyCache.Value;
        }

        var cycle = GetCircularDependencyPath();
        _hasCircularDependencyCache = cycle.Count > 0;
        return _hasCircularDependencyCache.Value;
    }

    /// <summary>
    /// Gets the path of nodes forming a circular dependency
    /// </summary>
    /// <returns>List of nodes in the circular dependency path, or empty list if none found</returns>
    public List<Node> GetCircularDependencyPath()
    {
        if (_circularDependencyPathCache != null)
        {
            return _circularDependencyPathCache;
        }

        _circularDependencyPathCache = RootNode.DetectCircularDependency();
        return _circularDependencyPathCache;
    }

    /// <summary>
    /// Gets a formatted string representation of the circular dependency path
    /// </summary>
    /// <returns>String describing the circular dependency, or empty string if none found</returns>
    public string GetCircularDependencyPathString()
    {
        var cycle = GetCircularDependencyPath();
        if (cycle.Count == 0)
        {
            return string.Empty;
        }

        var pathBuilder = new System.Text.StringBuilder();
        pathBuilder.AppendLine("Circular dependency detected:");
        
        for (int i = 0; i < cycle.Count; i++)
        {
            var node = cycle[i];
            if (node is ModuleDeclarationNode moduleNode)
            {
                pathBuilder.Append($"  {moduleNode.Module.Name}");
                if (i < cycle.Count - 1)
                {
                    pathBuilder.Append(" -> ");
                }
            }
        }
        
        // Close the cycle by showing it returns to the first module
        if (cycle.Count > 0 && cycle[0] is ModuleDeclarationNode firstModule)
        {
            pathBuilder.Append($" -> {firstModule.Module.Name}");
        }
        
        return pathBuilder.ToString();
    }
}