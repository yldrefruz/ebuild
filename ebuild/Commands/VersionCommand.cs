using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace ebuild.Commands;

/// <summary>
/// Command to display the current version of ebuild
/// </summary>
[Command("version", Description = "Display the current version of ebuild")]
public class VersionCommand : BaseCommand
{
    public override ValueTask ExecuteAsync(IConsole console)
    {
        base.ExecuteAsync(console);

        var version = UpdateManager.GetCurrentVersion();
        var versionString = UpdateManager.GetCurrentVersionString();

        console.Output.WriteLine($"ebuild version {versionString}");
        console.Output.WriteLine($"Assembly version: {version}");

        return ValueTask.CompletedTask;
    }
}
