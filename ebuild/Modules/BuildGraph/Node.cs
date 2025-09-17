using ebuild.api;

namespace ebuild.Modules.BuildGraph;


public class Node(string name)
{
    public string Name = name;
    public AccessLimitList<Node> Children = new();
    public Node? Parent = null;

    public void AddChild(Node child, AccessLimit accessLimit = AccessLimit.Public)
    {
        Children.Add(accessLimit, child);
        child.Parent = this;
    }
    
    public virtual Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs a depth-first search to detect circular dependencies among module nodes
    /// </summary>
    /// <param name="visited">Set of nodes currently being visited in this path</param>
    /// <param name="path">Current path being explored</param>
    /// <returns>List of nodes forming the circular dependency, or empty list if none found</returns>
    public List<Node> DetectCircularDependency(HashSet<Node>? visited = null, List<Node>? path = null)
    {
        visited ??= new HashSet<Node>();
        path ??= new List<Node>();

        // If we've already visited this node in the current path, we found a cycle
        if (visited.Contains(this))
        {
            var cycleStart = path.IndexOf(this);
            if (cycleStart >= 0)
            {
                var cycle = path.GetRange(cycleStart, path.Count - cycleStart);
                cycle.Add(this); // Add the node that closes the cycle
                return cycle;
            }
        }

        visited.Add(this);
        path.Add(this);

        // Check all children (both public and private dependencies)
        foreach (var child in Children.Joined())
        {
            if (child is ModuleDeclarationNode) // Only check module dependencies for cycles
            {
                var cycle = child.DetectCircularDependency(visited, path);
                if (cycle.Count > 0)
                {
                    return cycle;
                }
            }
        }

        visited.Remove(this);
        path.RemoveAt(path.Count - 1);
        return [];
    }
}
