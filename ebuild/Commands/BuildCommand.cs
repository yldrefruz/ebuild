using System.CommandLine;
using System.Text.Json.Nodes;
using CliFx.Attributes;
using ebuild.api;
using ebuild.Modules.BuildGraph;



namespace ebuild.Commands
{
    [Command("build", Description = "build the specified module")]
    public class BuildCommand : ModuleCreatingCommand
    {
    
    
        [CommandOption("dry-run", 'n', Description = "perform a trial run with no actual building")]
        public bool NoCompile { get; init; } = false;
        [CommandOption("clean", Description = "clean build")]
        public bool Clean { get; init; } = false;
        [CommandOption("build-worker-count", 'p', Description = "the build worker count to use. Default is 1")]
        public int ProcessCount { get; init; } = 1;



        public override async ValueTask ExecuteAsync(CliFx.Infrastructure.IConsole console)
        {
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var moduleInstance = (await moduleFile.CreateModuleInstance(ModuleInstancingParams)) ?? throw new Exception("Failed to create module instance");
            var graph = new Graph(moduleInstance);
            var worker = graph.CreateWorker<BuildWorker>();
            worker.MaxWorkerCount = ProcessCount;
            //TODO: Clean build if specified so.
            await (worker as IWorker).ExecuteAsync();

        }
    }
}