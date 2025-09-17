using ebuild.api;
using ebuild.BuildGraph;

namespace ebuild.Modules.BuildGraph;

public class BuildWorker(Graph graph) : IWorker
{
    public Graph WorkingGraph { get; init; } = graph;
    ModuleBase Module => this.WorkingGraph.Module;
    public Dictionary<string, object?> GlobalMetadata { get; init; } = [];
    public int MaxWorkerCount{ get; set; } = 1;

    public async Task WorkOnNodesAsync(List<Node> nodes, CancellationToken cancellationToken)
    {
        // 1st run compilation nodes in parallel
        await Parallel.ForEachAsync(
            nodes.Where(n => n is CompileSourceFileNode),
            new ParallelOptions { MaxDegreeOfParallelism = MaxWorkerCount, CancellationToken = cancellationToken },
            async (node, _) => await node.ExecuteAsync(this, cancellationToken)
        );
        // 2nd run link node
        foreach (var node in nodes.Where(n => n is LinkerNode))
        {
            await node.ExecuteAsync(this, cancellationToken);
        }
        // 3rd run additional dependency nodes in parallel
        // TODO: Additional dependency nodes.
    }
}
