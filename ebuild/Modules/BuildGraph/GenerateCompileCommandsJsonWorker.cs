using ebuild.api;
using ebuild.BuildGraph;

namespace ebuild.Modules.BuildGraph;

public class GenerateCompileCommandsJsonWorker(Graph graph) : IWorker
{
    public Graph WorkingGraph { get; init; } = graph;
    ModuleBase Module => WorkingGraph.Module;
    public Dictionary<string, object?> GlobalMetadata { get; init; } = [];
    public int MaxWorkerCount { get; set; } = 1;

    public async Task WorkOnNodesAsync(List<Node> nodes, CancellationToken cancellationToken)
    {
        // Only work on compilation nodes. As we don't use the linker or other nodes in `compile_commands.json`.
        await Parallel.ForEachAsync(
            nodes.Where(n => n is CompileSourceFileNode && (GlobalMetadata.TryGetValue("target_module", out object? targetModuleObj) ? (n.Parent as ModuleDeclarationNode)?.Module == (ModuleBase?)targetModuleObj : true)),
            new ParallelOptions { MaxDegreeOfParallelism = MaxWorkerCount, CancellationToken = cancellationToken },
            async (node, _) => await node.ExecuteAsync(this, cancellationToken)
        );
    }
}
