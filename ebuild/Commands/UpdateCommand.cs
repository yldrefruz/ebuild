using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace ebuild.Commands;

/// <summary>
/// Command to check for and apply updates to ebuild
/// </summary>
[Command("update", Description = "Check for and apply updates to ebuild")]
public class UpdateCommand : BaseCommand
{
    [CommandOption("check-only", Description = "Only check for updates without applying them")]
    public bool CheckOnly { get; init; } = false;

    [CommandOption("force", Description = "Force update even if already on latest version")]
    public bool Force { get; init; } = false;

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);

        var updateManager = new UpdateManager();
        
        // Check for updates
        var (isAvailable, latestVersion, downloadUrl, releaseNotes, sha256) = await updateManager.CheckForUpdateAsync();

        var currentVersion = UpdateManager.GetCurrentVersionString();
        console.Output.WriteLine($"Current version: {currentVersion}");

        if (!isAvailable && !Force)
        {
            console.Output.WriteLine("You are already running the latest version.");
            return;
        }

        if (isAvailable)
        {
            console.Output.WriteLine($"Latest version: {latestVersion}");
            console.Output.WriteLine();
            
            if (!string.IsNullOrEmpty(releaseNotes))
            {
                console.Output.WriteLine("Release notes:");
                console.Output.WriteLine(releaseNotes);
                console.Output.WriteLine();
            }
        }

        if (CheckOnly)
        {
            if (isAvailable)
            {
                console.Output.WriteLine("An update is available. Run 'ebuild update' to install it.");
            }
            return;
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            console.Error.WriteLine("Error: Could not find download URL for your platform.");
            return;
        }

        // Prompt for confirmation
        console.Output.WriteLine("Do you want to download and install the update? (y/n)");
        
        // Read from console input
        string? response = null;
        try
        {
            response = console.Input.ReadLine();
        }
        catch
        {
            // Fallback to System.Console for compatibility
            response = Console.ReadLine();
        }
        
        if (response?.Trim().ToLowerInvariant() != "y")
        {
            console.Output.WriteLine("Update cancelled.");
            return;
        }

        // Apply update
        var success = await updateManager.ApplyUpdateAsync(downloadUrl, sha256);
        
        if (success)
        {
            console.Output.WriteLine();
            console.Output.WriteLine("✓ Update completed successfully!");
            console.Output.WriteLine("Please restart ebuild to use the new version.");
        }
        else
        {
            console.Error.WriteLine("✗ Update failed. Please check the logs for details.");
        }
    }
}
