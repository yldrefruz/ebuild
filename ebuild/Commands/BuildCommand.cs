using System.CommandLine;
using CliFx.Attributes;
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
        
            var filePath = Path.GetFullPath(ModuleInstancingParams.SelfModuleReference.GetFilePath());

            var workDir = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
            // TODO: don't touch the current directory. Make all paths absolute instead.
            Directory.SetCurrentDirectory(workDir!);
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var moduleInstance = (await moduleFile.CreateModuleInstance(ModuleInstancingParams)) ?? throw new Exception("Failed to create module instance");
            var graph = new Graph(moduleInstance);
            var worker = graph.CreateWorker();
            worker.MaxWorkerCount = ProcessCount;
            //TODO: Clean build if specified so.
            await worker.ExecuteAsync();
        }
    }
}