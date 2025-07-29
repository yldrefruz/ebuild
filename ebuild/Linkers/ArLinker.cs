using System.Diagnostics;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

[Linker("Ar")]
public class ArLinker : LinkerBase
{
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("AR Linker");

    private string _arPath = string.Empty;

    public override bool IsAvailable(PlatformBase platform)
    {
        // Check if platform is Unix
        if (platform.GetName() != "Unix")
            return false;
            
        // Check if ar is actually available on the system
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ar",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // ar not found or not executable
        }
        
        return false;
    }

    public override async Task<bool> Setup()
    {
        // Try to find ar in common locations
        var arPaths = new[]
        {
            "/usr/bin/ar",
            "/usr/local/bin/ar",
            "/bin/ar"
        };

        foreach (var path in arPaths)
        {
            if (File.Exists(path))
            {
                _arPath = path;
                Logger.LogInformation("Found AR at: {path}", path);
                return true;
            }
        }

        // Try to find ar in PATH
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "ar",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    _arPath = output.Trim();
                    Logger.LogInformation("Found AR in PATH: {path}", _arPath);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Could not locate ar using 'which' command: {message}", ex.Message);
        }

        Logger.LogError("AR linker not found");
        return false;
    }

    public override async Task<bool> Link()
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for linking");
            return false;
        }

        if (CurrentModule.Type != ModuleType.StaticLibrary)
        {
            Logger.LogError("ArLinker can only handle static libraries");
            return false;
        }

        if (string.IsNullOrEmpty(_arPath))
        {
            Logger.LogError("AR linker path not set");
            return false;
        }

        Logger.LogInformation("Linking static library module {moduleName}", CurrentModule.Name);

        try
        {
            var outputDir = GetBinaryOutputFolder();
            Directory.CreateDirectory(outputDir);

            // Build ar command for static library
            var arCommand = BuildArCommand();
            if (string.IsNullOrEmpty(arCommand))
            {
                Logger.LogError("Failed to build ar command");
                return false;
            }

            Logger.LogInformation("Executing: {command}", arCommand);

            var startInfo = new ProcessStartInfo
            {
                FileName = "sh",
                Arguments = $"-c \"{arCommand}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = CurrentModule.Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory()
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Logger.LogError("Failed to start ar process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Logger.LogError("AR linking failed with exit code: {exitCode}", process.ExitCode);
                if (!string.IsNullOrEmpty(error))
                {
                    Logger.LogError("Linker error: {error}", error);
                }
                return false;
            }

            if (!string.IsNullOrEmpty(output))
            {
                Logger.LogInformation("{output}", output);
            }

            Logger.LogInformation("Linking successful");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Linking failed with exception: {message}", ex.Message);
            return false;
        }
    }

    private string BuildArCommand()
    {
        if (CurrentModule == null) throw new NullReferenceException("CurrentModule is null");

        // Get object files based on source files that the module created
        var objectFiles = new List<string>();
        var objectDir = GetObjectOutputFolder();
        
        foreach (var sourceFile in CurrentModule.SourceFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            var objectFile = Path.Combine(objectDir, $"{fileName}.obj");
            if (File.Exists(objectFile))
            {
                objectFiles.Add(objectFile);
            }
        }
        
        if (objectFiles.Count == 0)
        {
            Logger.LogError("No object files found for linking");
            return string.Empty;
        }

        var outputDir = GetBinaryOutputFolder();
        Directory.CreateDirectory(outputDir);

        ArgumentBuilder args = new();
        args += _arPath;
        args += "rcs";
        args += Path.Combine(outputDir, (CurrentModule.Name ?? "output") + ".a");
        args += objectFiles;

        return args.ToString();
    }

    private string GetObjectOutputFolder()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");

        return LinkerUtils.GetObjectOutputFolder(CurrentModule);
    }

    private string GetBinaryOutputFolder()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");
        
        return LinkerUtils.GetBinaryOutputFolder(CurrentModule);
    }

    public override string GetExecutablePath()
    {
        return _arPath;
    }
}