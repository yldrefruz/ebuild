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
        
        // Add standard flags
        args += "-std=c++17"; // Default to C++17, could be made configurable
        args += "-Wall"; // Enable all warnings
        args += "-Wextra"; // Enable extra warnings
        
        // Add debug or release flags based on configuration
        if (CurrentModule.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
        {
            args += "-g"; // Debug symbols
            args += "-O0"; // No optimization
        }
        else
        {
            args += "-O2"; // Optimize for speed
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

            // Generate compile command using the new method
            var baseCommand = GenerateCompileCommand(false);
            var arguments = new List<string>();
            
            // Parse base command and add to arguments
            var commandParts = baseCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            arguments.AddRange(commandParts);
            
            // Add source files
            arguments.AddRange(sourceFiles.Select(s => GetModuleFilePath(s, CurrentModule)));
            
            // Set output file based on module type
            var outputFile = Path.Combine(outputDir, CurrentModule.Name ?? "output");
            switch (CurrentModule.Type)
            {
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    arguments.Add("-o");
                    arguments.Add(outputFile);
                    break;
                case ModuleType.StaticLibrary:
                    arguments.Add("-c"); // Compile only for static library
                    arguments.Add("-o");
                    arguments.Add(outputFile + ".a");
                    break;
                case ModuleType.SharedLibrary:
                    arguments.Add("-shared");
                    arguments.Add("-fPIC"); // Position independent code
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
        var match = GccMessageRegex.Match(output);
        if (match.Success)
        {
            var file = match.Groups["file"].Value;
            var line = match.Groups["line"].Value;
            var column = match.Groups["column"].Value;
            var type = match.Groups["type"].Value;
            var message = match.Groups["message"].Value;

            switch (type)
            {
                case "error":
                    Logger.LogError("{file}:{line}:{column}: {type}: {message}", file, line, column, type, message);
                    break;
                case "warning":
                    Logger.LogWarning("{file}:{line}:{column}: {type}: {message}", file, line, column, type, message);
                    break;
                case "note":
                    Logger.LogInformation("{file}:{line}:{column}: {type}: {message}", file, line, column, type, message);
                    break;
                default:
                    Logger.LogInformation("{output}", output);
                    break;
            }
        }
        else
        {
            // If the regex doesn't match, log the raw output
            Logger.LogInformation("{output}", output);
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
