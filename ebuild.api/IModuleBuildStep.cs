namespace ebuild.api;




public interface IModuleBuildStep
{
    Task ExecuteAsync(Type WorkerType, CancellationToken cancellationToken);
}