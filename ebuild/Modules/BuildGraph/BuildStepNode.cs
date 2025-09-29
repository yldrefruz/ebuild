using ebuild.api;

namespace ebuild.Modules.BuildGraph;





public class BuildStepNode(string name, ModuleBuildStep step, BuildStepNode.StepType inStepType) : Node(name)
{
    public enum StepType
    {
        PreBuild,
        PostBuild
    }

    public readonly StepType stepType = inStepType;
    private readonly ModuleBuildStep step = step;
    public override async Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
    {
        await step.ExecuteAsync(worker.GetType(), cancellationToken);
    }
}