namespace ebuild.Modules.BuildGraph;



public interface IWorker
{
    public Graph WorkingGraph { get; }
    Dictionary<string, object?> GlobalMetadata { get; }
    public int MaxWorkerCount { get; }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var currentNodes = new List<Node> { WorkingGraph.GetRootNode() };
        await ExecuteNodesTraversalAsync(currentNodes, cancellationToken);
        return;
    }
    private async Task ExecuteNodesTraversalAsync(List<Node> nodes, CancellationToken cancellationToken)
    {
        // Pre order traversal
        foreach (var node in nodes)
        {
            if (node.Children.Joined().Count > 0)
            {
                var childNodes = node.Children.Joined().ToList();
                await ExecuteNodesTraversalAsync(childNodes, cancellationToken);
            }
        }
        await WorkOnNodesAsync(nodes, cancellationToken);
    }

    protected Task WorkOnNodesAsync(List<Node> nodes, CancellationToken cancellationToken);
}