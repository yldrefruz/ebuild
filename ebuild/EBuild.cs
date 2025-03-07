using System.CommandLine;
using System.CommandLine.Builder;
using System.Reflection;
using ebuild.api;
using ebuild.Commands;
using ebuild.Compilers;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;


namespace ebuild;

public static class EBuild
{
    public static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        builder
            .AddConsole().AddSimpleConsole(options => { options.SingleLine = true; }));

    public static string? FindEBuildApiDllPath()
    {
        return typeof(ModuleBase).Assembly.Location; // ModuleBase is in ebuild.api
    }

    public static async Task<int> Main(string[] args)
    {
        PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
        CompilerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);


        var rootCommand = new RootCommand
        {
            new BuildCommand(),
            new GenerateCommand(),
            new PropertyCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }
}