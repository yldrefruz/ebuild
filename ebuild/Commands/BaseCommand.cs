using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace ebuild.Commands;


public abstract class BaseCommand : ICommand
{
    [CommandOption("verbose", 'v', Description = "enable verbose logging")]
    public bool Verbose { get; init; } = false;

    public BaseCommand()
    {
    }

    public virtual ValueTask ExecuteAsync(IConsole console)
    {
        if (Verbose)
            EBuild.VerboseEnabled = true;
        return ValueTask.CompletedTask;
    }
}