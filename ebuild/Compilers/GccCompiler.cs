using System.Diagnostics;
using System.Text;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Compilers;

[Compiler("Gcc")]
public class GccCompiler : CompilerBase
{
    private static readonly ILogger Logger =
        LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = true;
            }))
            .CreateLogger("GCC Compiler");

    private string _gccPath = string.Empty;

    public override bool IsAvailable(PlatformBase platform)
    {
        return platform.GetName() == "Linux";
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        // Basic implementation - return empty list for now
        return new List<ModuleBase>();
    }

    public override Task<bool> Generate(string type, object? data = null)
    {
        // Basic implementation - no generation support for now
        return Task.FromResult(false);
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

        Logger.LogError("GCC compiler not found");
        return false;
    }

    public override async Task<bool> Compile()
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for compilation");
            return false;
        }

        if (string.IsNullOrEmpty(_gccPath))
        {
            Logger.LogError("GCC compiler path not set");
            return false;
        }

        Logger.LogInformation("Compiling module {moduleName}", CurrentModule.Name);

        try
        {
            // Basic compilation logic
            var sourceFiles = CurrentModule.SourceFiles;
            if (sourceFiles.Count == 0)
            {
                Logger.LogWarning("No source files to compile");
                return true;
            }

            // Create output directory
            var outputDir = GetBinaryOutputFolder();
            Directory.CreateDirectory(outputDir);

            // Basic gcc compilation command
            var arguments = new List<string>();
            
            // Add source files
            arguments.AddRange(sourceFiles);
            
            // Add include directories
            foreach (var include in CurrentModule.Includes.Joined())
            {
                arguments.Add($"-I{include}");
            }
            
            // Add definitions
            foreach (var definition in CurrentModule.Definitions.Joined())
            {
                arguments.Add($"-D{definition}");
            }
            
            // Add additional compiler options
            arguments.AddRange(AdditionalCompilerOptions);
            
            // Set output file
            var outputFile = Path.Combine(outputDir, CurrentModule.Name ?? "output");
            switch (CurrentModule.Type)
            {
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    arguments.Add("-o");
                    arguments.Add(outputFile);
                    break;
                case ModuleType.StaticLibrary:
                    arguments.Add("-c"); // Compile only
                    arguments.Add("-o");
                    arguments.Add(outputFile + ".a");
                    break;
                case ModuleType.SharedLibrary:
                    arguments.Add("-shared");
                    arguments.Add("-o");
                    arguments.Add(outputFile + ".so");
                    break;
            }

            var argumentString = string.Join(" ", arguments);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _gccPath,
                Arguments = argumentString,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = CurrentModule.Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory()
            };

            Logger.LogInformation("Executing: {command} {arguments}", _gccPath, argumentString);

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Logger.LogError("Failed to start GCC process");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Logger.LogInformation("GCC Output: {output}", output);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Logger.LogError("GCC Error: {error}", error);
            }

            if (process.ExitCode != 0)
            {
                Logger.LogError("GCC compilation failed with exit code: {exitCode}", process.ExitCode);
                return false;
            }

            Logger.LogInformation("Compilation successful");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Compilation failed with exception: {message}", ex.Message);
            return false;
        }
    }

    public override string GetExecutablePath()
    {
        return _gccPath;
    }

    private string GetBinaryOutputFolder()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");
        
        return CurrentModule.GetBinaryOutputDirectory();
    }
}