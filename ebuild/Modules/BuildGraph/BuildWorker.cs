using CliFx.Exceptions;
using ebuild.api;
using ebuild.BuildGraph;
using Microsoft.Extensions.Logging;

namespace ebuild.Modules.BuildGraph;

public class BuildWorker(Graph graph) : IWorker
{
    public Graph WorkingGraph { get; init; } = graph;
    ModuleBase Module => this.WorkingGraph.Module;
    public Dictionary<string, object?> GlobalMetadata { get; init; } = [];
    public int MaxWorkerCount { get; set; } = 1;
    public ILogger Logger { get; init; } = EBuild.LoggerFactory.CreateLogger<BuildWorker>();

    public async Task WorkOnNodesAsync(List<Node> nodes, CancellationToken cancellationToken)
    {
        // Run pre-build steps non-parallel
        {
            foreach (var step in nodes.Where(n => n is BuildStepNode bsn && bsn.stepType == BuildStepNode.StepType.PreBuild).Cast<BuildStepNode>())
            {
                try
                {
                    Logger.LogDebug("Running pre-build step \"{StepName}\"", step.Name);
                    using var scopeLogger = Logger.BeginScope("Pre-build step \"{StepName}\"", step.Name);
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
        // 2.1 copy shared libraries non-parallel
        foreach (var node in nodes.Where(n => n is CopySharedLibraryToRootModuleBinNode))
        {
            try
            {
                await node.ExecuteAsync(this, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new CliFxException($"Copying shared library failed: {ex.Message}", 1, false, ex);
            }
        }
        // 3rd run additional dependency nodes non-parallel
        foreach (var node in nodes.Where(n => n is AdditionalDependencyNode).Cast<AdditionalDependencyNode>())
        {
            try
            {
                await node.ExecuteAsync(this, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new CliFxException($"Processing additional dependency failed: {ex.Message}", 1, false, ex);
            }
        }

        // 4th run post-build steps non-parallel
        foreach (var step in nodes.Where(n => n is BuildStepNode bsn && bsn.stepType == BuildStepNode.StepType.PostBuild).Cast<BuildStepNode>())
        {
            try
            {
                Logger.LogDebug("Running post-build step \"{StepName}\"", step.Name);
                using var scopeLogger = Logger.BeginScope("Post-build step \"{StepName}\"", step.Name);
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
