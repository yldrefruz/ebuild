using System.CommandLine;
using CliFx.Attributes;
using ebuild.api;
using ebuild.api.Toolchain;
using ebuild.Compilers;



namespace ebuild.Commands;

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
        // TODO: Move to a build graph system.
        // TODO: Create a build graph and make it abstract so that the compiler isn't responsible for managing the build order and module itself.
        // TODO: Linker shouldn'be called from the compiler. Build graph should manage the linkers as well.
        var compiler = await ModuleInstancingParams.Toolchain.CreateCompiler(moduleInstance, ModuleInstancingParams);
        if (compiler == null)
            return;
        compiler.CleanCompilation = Clean;
        compiler.ProcessCount = ProcessCount; 
        if (!NoCompile)
            await compiler.Compile();
    }
}