namespace ebuild.api;




public class ModuleBuildStep(string name, ModuleBuildStep.ModuleBuildStepDelegate? onExecute = null)
{
    public delegate Task ModuleBuildStepDelegate(Type WorkerType, CancellationToken cancellationToken);
    // Can be overriden
    public virtual Task ExecuteAsync(Type WorkerType, CancellationToken cancellationToken)
    {
        if (OnExecute is not null)
        {
            return OnExecute(WorkerType, cancellationToken);
        }
        return Task.CompletedTask;
    }
    public string Name = name;
    public ModuleBuildStepDelegate? OnExecute = onExecute;
}