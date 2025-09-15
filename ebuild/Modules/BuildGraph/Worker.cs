using ebuild.api;
using ebuild.BuildGraph;

namespace ebuild.Modules.BuildGraph
{
    class Worker(Graph graph)
    {
        Graph Graph = graph;
        ModuleBase Module => Graph.Module;
        Dictionary<string, object?> GlobalMetadata = [];
        public int MaxWorkerCount = 1;


        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var currentNodes = new List<Node> { Graph.GetRootNode() };
            await ExecuteNodesAsync(currentNodes, cancellationToken);
            return;
        }


        private async Task ExecuteNodesAsync(List<Node> nodes, CancellationToken cancellationToken)
        {
            // Pre order traversal
            foreach (var node in nodes)
            {
                if (node.Children.Joined().Count > 0)
                {
                    var childNodes = node.Children.Joined().ToList();
                    await ExecuteNodesAsync(childNodes, cancellationToken);
                }
            }
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

        }
    }
}
