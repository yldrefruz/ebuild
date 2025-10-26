namespace ebuild.api;




/// <summary>
/// Represents a custom build step that can be executed as part of a module's build pipeline.
/// Module authors can create instances with an optional delegate to run custom logic during
/// the build (for example copying files, running generators, or invoking tools).
/// </summary>
/// <param name="name">A short, human-readable name for the build step.</param>
/// <param name="onExecute">An optional delegate that will be invoked when the step executes.</param>
public class ModuleBuildStep(string name, ModuleBuildStep.ModuleBuildStepDelegate? onExecute = null)
{
    /// <summary>
    /// Delegate signature used by <see cref="ModuleBuildStep"/> to execute custom work.
    /// The delegate receives the worker type (the runtime that will perform work) and a
    /// <see cref="CancellationToken"/> to support cooperative cancellation.
    /// </summary>
    /// <param name="WorkerType">A <see cref="Type"/> that indicates the worker responsible for executing the step.</param>
    /// <param name="cancellationToken">A cancellation token that should be observed during long-running work.</param>
    public delegate Task ModuleBuildStepDelegate(Type WorkerType, CancellationToken cancellationToken);

    /// <summary>
    /// Execute the build step asynchronously. By default this calls the <see cref="OnExecute"/>
    /// delegate if one was provided when the <see cref="ModuleBuildStep"/> was created. Derived
    /// classes may override this method to implement custom synchronous or asynchronous behaviour.
    /// </summary>
    /// <param name="WorkerType">The worker type used to perform the step.</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
    /// <returns>A task that completes when the step's work is finished.</returns>
    // Can be overriden
    public virtual Task ExecuteAsync(Type WorkerType, CancellationToken cancellationToken)
    {
        if (OnExecute is not null)
        {
            return OnExecute(WorkerType, cancellationToken);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Human-readable name of the build step.
    /// </summary>
    public string Name = name;

    /// <summary>
    /// Optional delegate invoked when <see cref="ExecuteAsync"/> runs. If <c>null</c>, the
    /// default implementation does nothing.
    /// </summary>
    public ModuleBuildStepDelegate? OnExecute = onExecute;
}