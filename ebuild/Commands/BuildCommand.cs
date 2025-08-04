using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using ebuild.Compilers;
using Microsoft.Extensions.Logging;

namespace ebuild.Commands;

public class BuildCommand
{
    private readonly ILogger _buildLogger = EBuild.LoggerFactory.CreateLogger("Build");

    private readonly Command _command = new("build", "build the specified module");
    private readonly Option<bool> _noCompile = new("--noCompile", () => false, "disable compilation");
    private readonly Option<bool> _clean = new("--clean", () => false, "clean compilation");

    private readonly Option<int> _processCount =
        new(new[] { "--process-count", "-pc" }, description: "the multi process count");

    private async Task Execute(InvocationContext context)
    {
        var compilerInstancingParams = ModuleInstancingParams.FromOptionsAndArguments(context);
        compilerInstancingParams.Logger = _buildLogger;
    
        var filePath = Path.GetFullPath(compilerInstancingParams.GetSelfModuleReference().GetFilePath());
        
        var workDir = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
        Directory.SetCurrentDirectory(workDir!);
        var compiler = await CompilerRegistry.CreateInstanceFor(compilerInstancingParams);
        if (compiler == null)
            return;
        var noCompile = context.ParseResult.GetValueForOption(_noCompile);
        compiler.CleanCompilation = context.ParseResult.GetValueForOption(_clean);
        compiler.ProcessCount = context.ParseResult.HasOption(_processCount)
            ? context.ParseResult.GetValueForOption(_processCount)
            : null;
        if (!noCompile)
            await compiler.Compile();
    }

    public BuildCommand()
    {
        _command.AddOption(_noCompile);
        _command.AddOption(_clean);
        _command.AddOption(_processCount);
        _command.AddCompilerCreationParams();

        _command.SetHandler(Execute);
    }

    public static implicit operator Command(BuildCommand c) => c._command;
}