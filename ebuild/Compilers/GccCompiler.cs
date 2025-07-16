using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Compilers;

[Compiler("Gcc")]
public class GccCompiler : CompilerBase
{
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("GCC Compiler");

    // Regex for parsing GCC output messages
    // Format: file.cpp:line:column: error/warning: message
    private static readonly Regex GccMessageRegex = new(@"^(?<file>.*):(?<line>\d+):(?<column>\d+): (?<type>error|warning|note): (?<message>.*)$");

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

    public override List<ModuleBase> FindCircularDependencies()
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> Generate(string type, object? data = null)
    {
        if (type == "CompileCommandsJSON")
        {
            return await GenerateCompileCommandsJson((string?)data);
        }
        
        return false;
    }

    private async Task<bool> GenerateCompileCommandsJson(string? outFile)
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for CompileCommandsJSON generation");
            return false;
        }

        var command = GenerateCompileCommand(false);
        
        // Add clang-specific flags for better IDE support
        command += " -D__GNUC__ -D__GNUG__";
        
        // Set the output file path
        var outputFile = outFile ?? "compile_commands.json";
        var outputPath = Path.IsPathFullyQualified(outputFile) 
            ? outputFile 
            : Path.Combine(CurrentModule.Context.ModuleDirectory?.FullName ?? "./", outputFile);
        
        try
        {
            var jsonEntries = CurrentModule.SourceFiles.Select(source => new JsonObject
            {
                { "directory", CurrentModule.Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory() },
                { "command", $"{_gccPath} {command} -c \"{GetModuleFilePath(source, CurrentModule)}\"" },
                { "file", GetModuleFilePath(source, CurrentModule) }
            });
            
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            
            var serialized = JsonSerializer.Serialize(jsonEntries, jsonSerializerOptions);
            await File.WriteAllTextAsync(outputPath, serialized);
            
            Logger.LogInformation("Generated compile_commands.json at: {outputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to generate compile_commands.json: {message}", ex.Message);
            return false;
        }
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

    private string GenerateCompileCommand(bool includeSourceFiles)
    {
        if (CurrentModule == null) throw new NullReferenceException("CurrentModule is null");
        
        ArgumentBuilder args = new();
        
        // Add standard flags based on module's CppStandard
        args += CppStandardToArg(CurrentModule.CppStandard);
        args += "-Wall"; // Enable all warnings
        args += "-Wextra"; // Enable extra warnings
        
        // Add debug or release flags based on configuration
        if (CurrentModule.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
        {
            args += "-g"; // Debug symbols
            args += OptimizationLevelToArg(OptimizationLevel.None); // No optimization in debug
        }
        else
        {
            args += OptimizationLevelToArg(CurrentModule.OptimizationLevel); // Use module's optimization level
            args += "-DNDEBUG"; // No debug assertions
        }
        
        // Add include directories
        foreach (var include in CurrentModule.Includes.Joined())
        {
            args += $"-I{GetModuleFilePath(include, CurrentModule)}";
        }
        
        // Add definitions
        foreach (var definition in CurrentModule.Definitions.Joined())
        {
            args += $"-D{definition}";
        }
        
        // Add force includes
        foreach (var forceInclude in CurrentModule.ForceIncludes.Joined())
        {
            args += $"-include";
            args += GetModuleFilePath(forceInclude, CurrentModule);
        }
        
        // Add building definition for current module
        if (includeSourceFiles)
        {
            args += $"-D{(CurrentModule.Name ?? CurrentModule.Context.ModuleDirectory!.Name).ToUpperInvariant()}_BUILDING";
        }
        
        // Add additional compiler options
        args += AdditionalCompilerOptions;
        
        // Add source files if requested
        if (includeSourceFiles)
        {
            args += CurrentModule.SourceFiles.Select(s => GetModuleFilePath(s, CurrentModule));
        }
        
        // Add dependency includes and definitions
        var binaryDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Directory.GetParent(binaryDir)!.FullName);
        
        var currentModuleFile = ModuleFile.Get(CurrentModule.Context.ModuleFile.FullName);
        var dependencyTree = currentModuleFile.GetDependencyTree();
        foreach (var moduleChild in dependencyTree.GetFirstLevelAndPublicDependencies())
        {
            var childModule = moduleChild.GetCompiledModule()!;
            args += childModule.Definitions.Public.Select(definition => $"-D{definition}");
            args += childModule.Includes.Public.Select(include => $"-I{GetModuleFilePath(include, childModule)}");
            args += childModule.ForceIncludes.Public.Select(forceInclude => 
            {
                var result = new List<string> { "-include", GetModuleFilePath(forceInclude, childModule) };
                return result;
            }).SelectMany(x => x);
        }
        
        Directory.SetCurrentDirectory(binaryDir);
        
        return args.ToString();
    }

    private static string GetModuleFilePath(string path, ModuleBase module)
    {
        var fp = Path.GetFullPath(path, module.Context.ModuleDirectory!.FullName);
        return fp;
    }

    private static string CppStandardToArg(CppStandards standard)
    {
        return standard switch
        {
            CppStandards.Cpp14 => "-std=c++14",
            CppStandards.Cpp17 => "-std=c++17",
            CppStandards.Cpp20 => "-std=c++20",
            CppStandards.CppLatest => "-std=c++2b", // Latest supported by GCC
            _ => "-std=c++20" // Default to C++20
        };
    }

    private static string OptimizationLevelToArg(OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => "-O0",
            OptimizationLevel.Size => "-Os",
            OptimizationLevel.Speed => "-O2",
            OptimizationLevel.Max => "-O3",
            _ => "-O2" // Default to speed optimization
        };
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
            // Check if we have source files to compile
            var sourceFiles = CurrentModule.SourceFiles;
            if (sourceFiles.Count == 0)
            {
                Logger.LogWarning("No source files to compile");
                return true;
            }

            // Create output directory
            var outputDir = GetBinaryOutputFolder();
            Directory.CreateDirectory(outputDir);

            // Generate compile command using GenerateCompileCommand(true) to include source files
            var commandContent = GenerateCompileCommand(true);
            
            // Add module type specific flags
            switch (CurrentModule.Type)
            {
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    commandContent += $" -o \"{Path.Combine(outputDir, CurrentModule.Name ?? "output")}\"";
                    break;
                case ModuleType.StaticLibrary:
                    commandContent += " -c"; // Compile only for static library
                    commandContent += $" -o \"{Path.Combine(outputDir, (CurrentModule.Name ?? "output") + ".a")}\"";
                    break;
                case ModuleType.SharedLibrary:
                    commandContent += " -shared -fPIC"; // Position independent code
                    commandContent += $" -o \"{Path.Combine(outputDir, (CurrentModule.Name ?? "output") + ".so")}\"";
                    break;
            }

            // Write command to a temporary file to avoid command length limits
            var commandFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(commandFilePath, commandContent);

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
            Logger.LogDebug("Command file content: {content}", commandContent);

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Logger.LogError("Failed to start GCC process");
                return false;
            }

            // Set up async reading of output and error streams
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    ParseGccOutput(args.Data);
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    ParseGccOutput(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
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

    private void ParseGccOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        var match = GccMessageRegex.Match(output);
        if (match.Success)
        {
            var file = match.Groups["file"].Value;
            var line = match.Groups["line"].Value;
            var column = match.Groups["column"].Value;
            var type = match.Groups["type"].Value;
            var message = match.Groups["message"].Value;

            // Format the message with colors
            var location = $"{file}:{line}:{column}";
            
            switch (type.ToLowerInvariant())
            {
                case "error":
                    Logger.LogError("\u001b[31m{location}: \u001b[1merror:\u001b[0m {message}", location, message);
                    break;
                case "warning":
                    Logger.LogWarning("\u001b[33m{location}: \u001b[1mwarning:\u001b[0m {message}", location, message);
                    break;
                case "note":
                    Logger.LogInformation("\u001b[36m{location}: \u001b[1mnote:\u001b[0m {message}", location, message);
                    break;
                default:
                    Logger.LogInformation("{location}: {type}: {message}", location, type, message);
                    break;
            }
        }
        else
        {
            // Handle other types of output (e.g., linker messages, general info)
            if (output.Contains("error:") || output.Contains("fatal error:"))
            {
                Logger.LogError("\u001b[31m{output}\u001b[0m", output);
            }
            else if (output.Contains("warning:"))
            {
                Logger.LogWarning("\u001b[33m{output}\u001b[0m", output);
            }
            else if (output.Contains("note:"))
            {
                Logger.LogInformation("\u001b[36m{output}\u001b[0m", output);
            }
            else
            {
                // General compiler output
                Logger.LogInformation("{output}", output);
            }
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
