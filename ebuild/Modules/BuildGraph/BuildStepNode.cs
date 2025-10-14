using ebuild.api;

namespace ebuild.Modules.BuildGraph;





public class BuildStepNode : Node
{
    public enum StepType
    {
        PreBuild,
        PostBuild
    }

    public readonly StepType stepType;
    private readonly ModuleBuildStep step;

    public BuildStepNode(ModuleBuildStep step, StepType inStepType) : base("BuildStep")
    {
        stepType = inStepType;
        this.step = step;

        if (inStepType is StepType.PreBuild)
            Name = $"PreBuildStep({step.Name})";
        else if (inStepType is StepType.PostBuild)
            Name = $"PostBuildStep({step.Name})";
        else
            Name = $"BuildStep({step.Name})";
    }

    public override async Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
    {
        await step.ExecuteAsync(worker.GetType(), cancellationToken);
    }
}