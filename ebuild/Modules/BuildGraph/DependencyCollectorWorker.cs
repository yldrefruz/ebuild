
using ebuild.api;

namespace ebuild.Modules.BuildGraph;




public class DependencyCollectorWorker(Graph graph) : IWorker
{
    public Graph WorkingGraph => graph;

    public Dictionary<string, object?> GlobalMetadata => [];

    public int MaxWorkerCount => 1;


    public HashSet<ModuleBase> AllDependencies { get; } = [];

    public async Task WorkOnNodesAsync(List<Node> nodes, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(nodes.Where(n => n is ModuleDeclarationNode),
        new ParallelOptions { MaxDegreeOfParallelism = MaxWorkerCount, CancellationToken = cancellationToken },
        async (node, _) =>
        {
            if (node is ModuleDeclarationNode moduleNode)
            {
                var module = moduleNode.Module;
                lock (AllDependencies)
                {
                    AllDependencies.Add(module);
                }
            }
            await Task.CompletedTask;
        });
    }
}