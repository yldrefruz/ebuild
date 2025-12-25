using System.CommandLine;
using System.Text.Json.Nodes;
using ebuild.api;
using ebuild.cli;
using ebuild.Modules.BuildGraph;



namespace ebuild.Commands
{
    [Command("build", Description = "build the specified module")]
    public class BuildCommand : ModuleCreatingCommand
    {


        [Option("dry-run", ShortName = "n", Description = "perform a trial run with no actual building")]
        public bool NoCompile = false;
        [Option("clean", Description = "clean build")]
        public bool Clean = false;
        [Option("build-worker-count", ShortName = "p", Description = "the build worker count to use. Default is 1")]
        public int ProcessCount = 1;


        public override async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(cancellationToken);
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var moduleInstance = (await moduleFile.CreateModuleInstance(ModuleInstancingParams)) ?? throw new Exception("Failed to create module instance");
            var graph = new Graph(moduleInstance);
            var worker = graph.CreateWorker<BuildWorker>();
            worker.Clean = Clean;
            worker.MaxWorkerCount = ProcessCount;
            await (worker as IWorker).ExecuteAsync(cancellationToken);
            return 0;
        }
    }
}