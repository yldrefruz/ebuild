using CliFx.Exceptions;
using ebuild.api;
using ebuild.BuildGraph;

namespace ebuild.Modules.BuildGraph;

public class BuildWorker(Graph graph) : IWorker
{
    public Graph WorkingGraph { get; init; } = graph;
    ModuleBase Module => this.WorkingGraph.Module;
    public Dictionary<string, object?> GlobalMetadata { get; init; } = [];
    public int MaxWorkerCount { get; set; } = 1;

    public async Task WorkOnNodesAsync(List<Node> nodes, CancellationToken cancellationToken)
    {
        // Run pre-build steps non-parallel
        {
            foreach (var step in nodes.Where(n => n is BuildStepNode bsn && bsn.stepType == BuildStepNode.StepType.PreBuild).Cast<BuildStepNode>())
            {
                try
                {
                    await step.ExecuteAsync(this, cancellationToken);
                }
                catch (Exception exception)
                {
                    // If there was an error promote it to clifx exception
                    throw new CliFxException($"Pre-build step \"{step.Name}\" failed: {exception.Message}", 1, false, exception);
                }
            }
        }
        // 1st run compilation nodes in parallel
        {
            var exceptions = new List<Exception>();
            await Parallel.ForEachAsync(
                nodes.Where(n => n is CompileSourceFileNode),
                new ParallelOptions { MaxDegreeOfParallelism = MaxWorkerCount, CancellationToken = cancellationToken },
                async (node, _) =>
                {
                    try
                    {
                        await node.ExecuteAsync(this, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }
            );
            if (exceptions.Count > 0)
            {
                // Aggregate all exceptions and throw as CliFxException
                throw new CliFxException(
                $"Compilation of {Module.Name} failed with {exceptions.Count} source file uncompiled(s): {string.Join("; ", exceptions.Select(e => e.Message))}",
                1,
                false,
                new AggregateException(exceptions)
                );
            }
        }
        // 2nd run link nodes non-parallel
        {
            foreach (var node in nodes.Where(n => n is LinkerNode))
            {
                try
                {
                    await node.ExecuteAsync(this, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new CliFxException($"Linking failed: {ex.Message}", 1, false, ex);
                }
            }
        }
        // 3rd run additional dependency nodes in parallel
        // TODO: #20 Additional dependency nodes.
        
        // 4th run post-build steps non-parallel
        foreach (var step in nodes.Where(n => n is BuildStepNode bsn && bsn.stepType == BuildStepNode.StepType.PostBuild).Cast<BuildStepNode>())
        {
            try
            {
                await step.ExecuteAsync(this, cancellationToken);
            }
            catch (Exception exception)
            {
                // If there was an error promote it to clifx exception
                throw new CliFxException($"Post-build step \"{step.Name}\" failed: {exception.Message}", 1, false, exception);
            }
        }

    }
}
