using System.Diagnostics;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

[Linker("Gcc")]
public class GccLinker : LinkerBase
{
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("GCC Linker");

    private string _gccPath = string.Empty;

    public override bool IsAvailable(PlatformBase platform)
    {
        // Check if platform is Unix
        if (platform.GetName() != "Unix")
            return false;
            
        // Check if gcc is actually available on the system
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gcc",
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
            // gcc not found or not executable
        }
        
        return false;
    }

    public override async Task<bool> Setup()
    {
        // Try to find gcc in common locations
        var gccPaths = new[]
        {
            "/usr/bin/gcc",
            "/usr/local/bin/gcc",
            "/bin/gcc"
        };

        foreach (var path in gccPaths)
        {
            if (File.Exists(path))
            {
                _gccPath = path;
                Logger.LogInformation("Found GCC at: {path}", path);
                return true;
            }
        }

        // Try to find gcc in PATH
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "gcc",
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
                    _gccPath = output.Trim();
                    Logger.LogInformation("Found GCC in PATH: {path}", _gccPath);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Could not locate gcc using 'which' command: {message}", ex.Message);
        }

        Logger.LogError("GCC linker not found");
        return false;
    }

    public override async Task<bool> Link()
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for linking");
            return false;
        }

        if (string.IsNullOrEmpty(_gccPath))
        {
            Logger.LogError("GCC linker path not set");
            return false;
        }

        Logger.LogInformation("Linking module {moduleName}", CurrentModule.Name);

        try
        {
            var outputDir = GetBinaryOutputFolder();
            Directory.CreateDirectory(outputDir);

            // Build linking command based on module type
            var linkCommand = BuildLinkCommand();

            // Write command to a temporary file to avoid command length limits
            var commandFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(commandFilePath, linkCommand);

            var startInfo = new ProcessStartInfo
            {
                FileName = _gccPath,
                Arguments = $"@{commandFilePath}", // Use command file
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = CurrentModule.Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory()
            };

            Logger.LogInformation("Executing: {command} @{commandFile}", _gccPath, commandFilePath);
            Logger.LogDebug("Command file content: {content}", linkCommand);

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Logger.LogError("Failed to start GCC linking process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Clean up command file
            if (File.Exists(commandFilePath))
            {
                try
                {
                    File.Delete(commandFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            if (process.ExitCode != 0)
            {
                Logger.LogError("GCC linking failed with exit code: {exitCode}", process.ExitCode);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Logger.LogError("Linker error: {error}", error);
                }
                return false;
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

    private string BuildLinkCommand()
    {
        if (CurrentModule == null) throw new NullReferenceException("CurrentModule is null");

        ArgumentBuilder args = new();
        
        // Add object files (assuming they exist from compilation)
        var objectFiles = Directory.GetFiles(GetObjectOutputFolder(), "*.o", SearchOption.TopDirectoryOnly);
        args += objectFiles;

        // Add library search paths
        foreach (var libPath in CurrentModule.LibrarySearchPaths.Joined())
        {
            args += $"-L{LinkerUtils.GetModuleFilePath(libPath, CurrentModule)}";
        }
        
        // Add dependency library search paths
        var currentModuleFile = ModuleFile.Get(CurrentModule.Context.ModuleFile.FullName);
        var dependencyTree = currentModuleFile.GetDependencyTree();
        foreach (var dependency in dependencyTree.GetFirstLevelAndPublicDependencies())
        {
            var compModule = dependency.GetCompiledModule()!;
            foreach (var libPath in compModule.LibrarySearchPaths.Public)
            {
                args += $"-L{LinkerUtils.GetModuleFilePath(libPath, compModule)}";
            }
        }

        // Add libraries
        foreach (var library in CurrentModule.Libraries.Joined())
        {
            if (File.Exists(Path.GetFullPath(library)))
            {
                args += Path.GetFullPath(library);
            }
            else
            {
                args += $"-l{library}";
            }
        }
        
        // Add dependency libraries
        foreach (var dependency in dependencyTree.GetFirstLevelAndPublicDependencies())
        {
            var compModule = dependency.GetCompiledModule()!;
            
            // Add dependency's output library
            switch (compModule.Type)
            {
                case ModuleType.SharedLibrary:
                    args += Path.Combine(compModule.GetBinaryOutputDirectory(), $"lib{compModule.Name}.so");
                    break;
                case ModuleType.StaticLibrary:
                    args += Path.Combine(compModule.GetBinaryOutputDirectory(), $"lib{compModule.Name}.a");
                    break;
            }
            
            // Add dependency's libraries
            foreach (var library in compModule.Libraries.Public)
            {
                var libPath = LinkerUtils.GetModuleFilePath(library, compModule);
                if (File.Exists(libPath))
                {
                    args += libPath;
                }
                else
                {
                    args += $"-l{library}";
                }
            }
        }

        // Add additional linker options
        args += AdditionalLinkerOptions;

        // Add module type specific flags and output
        var outputDir = GetBinaryOutputFolder();
        switch (CurrentModule.Type)
        {
            case ModuleType.Executable:
            case ModuleType.ExecutableWin32:
                args += $"-o {Path.Combine(outputDir, CurrentModule.Name ?? "output")}";
                break;
            case ModuleType.StaticLibrary:
                // For static libraries, we need to use ar instead of gcc
                return BuildArchiveCommand(objectFiles);
            case ModuleType.SharedLibrary:
                args += "-shared -fPIC";
                args += $"-o {Path.Combine(outputDir, (CurrentModule.Name ?? "output") + ".so")}";
                break;
        }

        return args.ToString();
    }

    private string BuildArchiveCommand(string[] objectFiles)
    {
        if (CurrentModule == null) throw new NullReferenceException("CurrentModule is null");

        ArgumentBuilder args = new();
        args += "ar";
        args += "rcs";
        args += Path.Combine(LinkerUtils.GetBinaryOutputFolder(CurrentModule), (CurrentModule.Name ?? "output") + ".a");
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

    private static string GetModuleFilePath(string path, ModuleBase module)
    {
        return LinkerUtils.GetModuleFilePath(path, module);
    }

    public override string GetExecutablePath()
    {
        return _gccPath;
    }
}