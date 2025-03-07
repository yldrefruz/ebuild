using System.CommandLine;
using System.CommandLine.Invocation;
using ebuild.api;
using ebuild.Compilers;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild.Commands;

public class BuildCommand
{
    private readonly ILogger _buildLogger = EBuild.LoggerFactory.CreateLogger("Build");

    private readonly Command _command = new("build", "build the specified module");
    private readonly Option<bool> _noCompile = new("--noCompile", () => false, "disable compilation");

    private async Task Execute(InvocationContext context)
    {
        var compilerInstancingParams = CompilerRegistry.CompilerInstancingParams.FromOptionsAndArguments(context);
        compilerInstancingParams.Logger = _buildLogger;
        var compiler = await CompilerRegistry.CreateInstanceFor(compilerInstancingParams);
        var noCompile = context.ParseResult.GetValueForOption(_noCompile);
        if (!noCompile)
            await compiler.Compile();
    }

    public BuildCommand()
    {
        _command.AddOption(_noCompile);
        _command.AddCompilerCreationParams();

        _command.SetHandler(Execute);
    }

    public static implicit operator Command(BuildCommand c) => c._command;
}