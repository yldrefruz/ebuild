using ebuild.api;

namespace ebuild.Modules.BuildGraph;


public class AdditionalDependencyNode(AdditionalDependency additionalDependency) : Node("AdditionalDependency:" + additionalDependency.DependencyPath)
{
    private AdditionalDependency additionalDependency = additionalDependency;

    public override Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
    {
        additionalDependency.Process(((BuildWorker)worker).WorkingGraph.Module);
        return Task.CompletedTask;
    }
}