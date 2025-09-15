using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;

namespace ebuild.Commands
{
    [Command("property", Description = "operations for properties. These are useful for creation of custom scripts or using ebuild without referencing, directly from command line")]
    public class PropertyCommand : ICommand
    {

        public virtual ValueTask ExecuteAsync(IConsole console)
        {
            throw new CommandException("You must specify a subcommand.");
        }
    }

    [Command("property get", Description = "get the value of a property")]
    public class PropertyGetCommand : PropertyCommand
    {
        [CommandParameter(0, Description = "the name of the property to get")]
        public string PropertyName { get; init; } = string.Empty;

        public override ValueTask ExecuteAsync(IConsole console)
        {
            if (PropertyName == "ebuild.api.dll")
            {
                console.Output.WriteLine(EBuild.FindEBuildApiDllPath());
            }
            else
            {
                throw new CommandException($"Unknown property '{PropertyName}'");
            }
            return ValueTask.CompletedTask;
        }
    }
}