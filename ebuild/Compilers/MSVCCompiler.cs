using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ebuild.api;
using ebuild.Linkers;
using Microsoft.Extensions.Logging;

namespace ebuild.Compilers;

[Compiler("Msvc")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class MsvcCompiler : CompilerBase
{
    private string _msvcCompilerRoot = string.Empty;
    private string _msvcToolRoot = string.Empty;

    private static readonly Regex CLMessageRegex = new(@"^(?<file>.*)\((?<location>\d+(?:,\d+)?)\) ?: (?<type>error|warning|note) ?(?<code>[A-Z]+\d+|)?: (?<message>.+)$");

    private static readonly ILogger Logger =
        LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = true;
            }))
            .CreateLogger("MSVC Compiler");



    string GetBinaryOutputFolder()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");
        return CurrentModule.GetBinaryOutputDirectory();
    }

    public override string GetExecutablePath()
    {
        var msvcCompilerBin = GetMsvcCompilerBin();
        var clPath = Path.Join(msvcCompilerBin, "cl.exe");
        if (clPath.Contains(' '))
        {
            clPath = "\"" + clPath + "\"";
        }

        return clPath;
    }

    private string GetMsvcCompilerBin()
    {
        var targetArch = "x86";
        if (CurrentModule is { Context.TargetArchitecture: Architecture.X64 })
            targetArch = "x64";
        var msvcCompilerBin = Path.Join(_msvcCompilerRoot, targetArch);
        return msvcCompilerBin;
    }

    private string GetMsvcCompilerLib()
    {
        var targetArch = "x86";
        if (CurrentModule is { Context.TargetArchitecture: Architecture.X64 })
            targetArch = "x64";
        return Path.Join(_msvcToolRoot, "lib", targetArch);
    }

    private static string GetModuleFilePath(string path, ModuleBase module)
    {
        var fp = Path.GetFullPath(path, module.Context.ModuleDirectory!.FullName);
        // We are in binary, so we should resolve the path from the binary folder.


        // TODO: This implementation doesn't make sense on the other context than building.
        // While trying to resolve include/force include paths, this gives the wrong result.


        // var rp = Path.GetRelativePath(Path.Join(module.Context.ModuleDirectory!.FullName, "Binaries"), path);
        // return fp.Length > rp.Length ? rp : fp;
        return fp;
    }

    private void MutateTarget()
    {
        if (CurrentModule == null)
            return;
        var windowsKitInfo = MSVCUtils.GetWindowsKit(CurrentModule.RequiredWindowsSdkVersion);
        CurrentModule.Includes.Private.Add(Path.Join(_msvcToolRoot, "include"));
        CurrentModule.LibrarySearchPaths.Private.Add(GetMsvcCompilerLib());
        if (windowsKitInfo != null)
        {
            var includes = new[] { "ucrt", "um", "winrt", "shared" };
            CurrentModule.Includes.Private.AddRange(includes.Select(include => Path.Join(windowsKitInfo.IncludePath, include)).Where(Directory.Exists));
            var searchPaths = new[] { "um", "ucrt", "ucrt_enclave" };
            if (CurrentModule.Context.TargetArchitecture == Architecture.X64)
            {
                searchPaths = searchPaths.Select(p => Path.Join(p, "x64")).ToArray();
            }
            else
            {
                searchPaths = searchPaths.Select(p => Path.Join(p, "x86")).ToArray();
            }
            CurrentModule.LibrarySearchPaths.Private.AddRange(searchPaths.Select(lib => Path.Join(windowsKitInfo.LibPath, lib)).Where(Directory.Exists));
        }
    }

    private string CppStandardToArg(CppStandards standard)
    {
        var value = "/std:";
        switch (standard)
        {
            case CppStandards.Cpp14:
                value += "c++14";
                break;
            case CppStandards.Cpp17:
                value += "c++17";
                break;
            default:
            case CppStandards.Cpp20:
                value += "c++20";
                break;
            case CppStandards.CppLatest:
                value += "c++latest";
                break;
        }

        return value;
    }

    private static string OptimizationLevelToArg(OptimizationLevel level)
    {
        return level switch
        {
            OptimizationLevel.None => "/Od",
            OptimizationLevel.Size => "/O1",
            OptimizationLevel.Speed => "/O2",
            OptimizationLevel.Max => "/Ox",
            _ => "/O2" // Default to speed optimization
        };
    }

    private void AddModuleCompileArguments(ModuleBase inputModule, bool compilingInputModule, ref ArgumentBuilder args,
        AccessLimit? accessLimit = null)
    {
        args += inputModule.Definitions.GetLimited(accessLimit).Select(definition => $"/D\"{definition}\"");

        args += inputModule.Includes.GetLimited(accessLimit).Select(include => $"/I\"{GetModuleFilePath(include, inputModule)}\"");
        args += inputModule.ForceIncludes.GetLimited(accessLimit).Select(s => $"/FI{GetModuleFilePath(s, inputModule)}");

        if (compilingInputModule)
        {
            args += $"/D\"{(inputModule.Name ?? inputModule.Context.ModuleDirectory!.Name).ToUpperInvariant()}_BUILDING\"";
            args += inputModule.CompilerOptions;
            // TODO: Disabled for now to test.
            // args += module.SourceFiles.Select(s => GetModuleFilePath(s, module));
        }
    }

    private string GenerateCompileCommand(bool forCompilation)
    {
        if (CurrentModule == null) throw new NullReferenceException();
        ArgumentBuilder args = new();
        // ReSharper disable once StringLiteralTypo
        args += "/nologo";
        args += "/c";
        args += "/EHsc";
        args += CppStandardToArg(CurrentModule.CppStandard);
        if (CurrentModule.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
        {
            args += "/MDd";
            args += "/Zi";
            args += $"/Fd\"{Path.Join(CompilerUtils.GetObjectOutputFolder(CurrentModule), CurrentModule.Name ?? CurrentModule.GetType().Name)}.pdb\"";
            args += "/FS";
            args += OptimizationLevelToArg(OptimizationLevel.None); // No optimization in debug
        }
        else
        {
            args += "/MD";
            args += OptimizationLevelToArg(CurrentModule.OptimizationLevel); // Use module's optimization level
        }
        if (forCompilation)
        {
            args += $"/Fo:";
            var objectOutputFolder = CompilerUtils.GetObjectOutputFolder(CurrentModule);

            // Ensure the directory exists before MSVC tries to use it
            try
            {
                if (!Directory.Exists(objectOutputFolder))
                {
                    Directory.CreateDirectory(objectOutputFolder);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create object output folder {folder}: {error}", objectOutputFolder, ex.Message);
                throw;
            }

            args += objectOutputFolder;
        }

        if (ProcessCount != null && forCompilation)
        {
            args += $"/MP{(ProcessCount <= 0 ? string.Empty : ProcessCount.ToString())}";
        }



        args += AdditionalCompilerOptions;

        AddModuleCompileArguments(CurrentModule, forCompilation, ref args);


        var binaryDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Directory.GetParent(binaryDir)!.FullName);


        var currentModuleFile = ModuleFile.Get(CurrentModule.Context.ModuleFile.FullName);
        var dependencyTree = currentModuleFile.GetDependencyTree();
        foreach (var moduleChild in dependencyTree.GetFirstLevelAndPublicDependencies())
        {
            // Append commands of the child module.
            AddModuleCompileArguments(moduleChild.GetCompiledModule()!, false, ref args, AccessLimit.Public);
        }

        Directory.SetCurrentDirectory(binaryDir);

        return args.ToString();
    }

    public override async Task<bool> Setup()
    {
        if (!MSVCUtils.VswhereExists())
        {
            if (!MSVCUtils.DownloadVsWhere())
            {
                throw new Exception(
                    $"Can't download vswhere from {MSVCUtils.VsWhereUrl}. Please check your internet connection.");
            }
        }

        var toolRoot = await MSVCUtils.GetMsvcToolRoot();

        if (string.IsNullOrEmpty(toolRoot))
        {
            Logger.LogInformation("MSVC tool root couldn't be found, MSVC Compiler and linker setup has failed");
            return false;
        }

        var version = await MSVCUtils.FindMsvcVersion(toolRoot, Logger);
        if (string.IsNullOrEmpty(version))
        {
            Logger.LogCritical("Couldn't find a valid msvc installation.");
            return false;
        }

        (_msvcToolRoot, _msvcCompilerRoot) = MSVCUtils.SetupMsvcPaths(toolRoot, version);
        return true;
    }

    public override async Task<bool> Compile()
    {
        if (CurrentModule == null) return false;
        Logger.LogInformation("Compiling module {moduleName}", CurrentModule.Name);
        foreach (var dependency in CurrentModule.Dependencies.Joined())
        {
            if (dependency == null) continue;
            // TODO: Compile the dependencies first.
            // Post-ordered compilation.
            IModuleInstancingParams createdParams = CurrentModule.Context.InstancingParams!.CreateCopyFor(dependency);
            CompilerBase? compiler = await CompilerRegistry.CreateInstanceFor(createdParams);
            if (compiler == null)
            {
                Logger.LogError("Compiler for dependency {dependency} is null.", dependency);
                return false;
            }
            await compiler.Setup();
            await compiler.Compile();
            Logger.LogInformation("Compiled dependency {dependency}", dependency);
        }
        MutateTarget();

        if (CleanCompilation)
        {
            //Delete all obj files before compiling
            ClearObjectAndPdbFiles(false);
        }

        // Compile each source file individually
        var sourceFiles = CurrentModule.SourceFiles;
        if (sourceFiles.Count == 0)
        {
            Logger.LogWarning("No source files to compile");
            return true;
        }

        var outputDir = CompilerUtils.GetObjectOutputFolder(CurrentModule);

        // Ensure the output directory exists with proper permissions
        try
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Logger.LogInformation("Created object output directory: {outputDir}", outputDir);
            }

            // Test write access to the directory
            var testFile = Path.Combine(outputDir, "test_write_access.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to create or access output directory {outputDir}: {error}", outputDir, ex.Message);
            throw new InvalidOperationException($"Cannot create or access output directory: {outputDir}", ex);
        }

        var errorFiles = new List<string>();

        // Compile each source file individually with antivirus-safe waiting
        Logger.LogInformation("Compiling {count} source files individually", sourceFiles.Count);

        foreach (var sourceFile in sourceFiles)
        {
            var success = await CompileSourceFileIndividually(sourceFile);
            if (!success)
            {
                errorFiles.Add(sourceFile);
            }
        }

        if (errorFiles.Count > 0)
        {
            Logger.LogError("Compilation failed for the following files: {files}", string.Join(", ", errorFiles));
            if (CleanCompilation)
            {
                ClearObjectAndPdbFiles();
            }
            Environment.ExitCode = 1;
            return false;
        }

        Logger.LogInformation("Successfully compiled all {count} source files", sourceFiles.Count);
        // Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName);

        // Use the linker if available, otherwise use default linker
        bool linkingSuccess = true;
        var linker = Linker ?? GetDefaultLinker();
        await linker.Setup();
        linker.SetModule(CurrentModule);
        linkingSuccess = await linker.Link();

        if (linkingSuccess)
        {
            ProcessAdditionalDependencies();
        }

        return linkingSuccess;
    }

    private async Task<bool> WaitForFileAccess(string filePath, int maxWaitSeconds)
    {
        if (maxWaitSeconds == 0)
        {
            return true; // No waiting
        }

        var startTime = DateTime.Now;
        var isInfiniteWait = maxWaitSeconds == -1;

        while (true)
        {
            try
            {
                // Try to create and immediately delete a test file in the same directory
                var testFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".",
                    $"ebuild_access_test_{Path.GetRandomFileName()}.tmp");

                File.WriteAllText(testFilePath, "test");
                File.Delete(testFilePath);

                Logger.LogDebug("File access confirmed for directory: {dir}", Path.GetDirectoryName(filePath));
                return true;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalSeconds;

                if (!isInfiniteWait && elapsed >= maxWaitSeconds)
                {
                    Logger.LogError("Timeout waiting for file access to {filePath} after {elapsed:F1} seconds. Last error: {error}",
                        filePath, elapsed, ex.Message);
                    return false;
                }

                Logger.LogDebug("Waiting for file access to {filePath} (elapsed: {elapsed:F1}s): {error}",
                    filePath, elapsed, ex.Message);

                await Task.Delay(100); // Wait 500ms before retrying
            }
        }
    }

    private async Task<bool> CompileSourceFileIndividually(string sourceFile)
    {
        if (CurrentModule == null) return false;

        var outputDir = CompilerUtils.GetObjectOutputFolder(CurrentModule);
        var objFileName = Path.GetFileNameWithoutExtension(sourceFile) + ".obj";
        var objFilePath = Path.Combine(outputDir, objFileName);

        // Wait for file access if configured
        var waitTime = Config.Get().WaitOutputFileMaxSeconds;
        if (waitTime != 0)
        {
            Logger.LogDebug("Checking file access for {sourceFile}", sourceFile);
            if (!await WaitForFileAccess(objFilePath, waitTime))
            {
                Logger.LogError("Failed to gain file access for {sourceFile}", sourceFile);
                return false;
            }
        }

        var commandContent = GenerateCompileCommand(true);
        commandContent += $" \"{GetModuleFilePath(sourceFile, CurrentModule)}\"";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, commandContent);

        var startInfo = new ProcessStartInfo()
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Arguments = $"@\"{tempFile}\"",
            FileName = GetExecutablePath(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            UseShellExecute = false,
        };

        Logger.LogDebug("Compiling {sourceFile}", Path.GetFileName(sourceFile));

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Logger.LogError("Failed to start compiler process for {sourceFile}", sourceFile);
            File.Delete(tempFile);
            return false;
        }
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                ParseMSVCCLOutput(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                ParseMSVCCLOutput(args.Data);
            }
        };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync();

        // Clean up temp file
        File.Delete(tempFile);

        if (process.ExitCode != 0)
        {
            return false;
        }

        Logger.LogDebug("Successfully compiled {sourceFile}", Path.GetFileName(sourceFile));
        return true;
    }

    private void ParseMSVCCLOutput(string output)
    {
        var match = CLMessageRegex.Match(output);
        if (match.Success)
        {
            var type = match.Groups["type"].Value;
            var code = match.Groups["code"].Value;
            var message = match.Groups["message"].Value;
            var file = match.Groups["file"].Value;
            var location = match.Groups["location"].Value;

            if (type == "error")
            {
                Logger.LogError("{file}({location}): {type} {code}: {message}", file, location, type, code, message);
            }
            else if (type == "warning")
            {
                Logger.LogWarning("{file}({location}): {type} {code}: {message}", file, location, type, code, message);
            }
            else
            {
                Logger.LogInformation("{line}", output);
            }
        }
        else
        {
            Logger.LogInformation("{line}", output);
        }

    }

    private void ClearObjectAndPdbFiles(bool shouldLog = true)
    {
        if (CurrentModule != null)
        {
            CompilerUtils.ClearObjectAndPdbFiles(CurrentModule, shouldLog);
        }
    }

    private void ProcessAdditionalDependencies()
    {
        Logger.LogInformation("Processing additional dependencies");
        foreach (var additionalDependency in CurrentModule!.AdditionalDependencies.Joined())
        {
            additionalDependency.Process(CurrentModule);
        }
    }

    public override async Task<bool> Generate(string what, Object? data = null)
    {
        if (what == "CompileCommandsJSON")
        {
            return await GenerateCompileDatabase((string?)data);
        }

        return false;
    }


    private async Task<bool> GenerateCompileDatabase(string? outFile)
    {
        var command = GenerateCompileCommand(false);
        command = command.Replace(@"\\", @"\");
        command += " /D__CLANGD__ "; // This is for making it work with clangd.
        if (CurrentModule == null)
            return false;
        switch (CurrentModule.CppStandard)
        {
            case CppStandards.Cpp14:
                command += "/D_MSVC_LANG=201402L ";
                break;
            case CppStandards.Cpp17:
                command += "/D_MSVC_LANG=201703L ";
                break;
            default:
            case CppStandards.Cpp20:
                command += "/D_MSVC_LANG=202002L ";
                break;
            case CppStandards.CppLatest:
                command += "/D_MSVC_LANG=202410L ";
                break;
        }
        List<JsonObject> jsonElements = [];
        jsonElements.AddRange(
            CurrentModule.SourceFiles.Select(source => new JsonObject
            {
                { "directory", Directory.GetCurrentDirectory() },
                { "command", GetExecutablePath() + " " + command + " " + $"\"{source}\"" },
                { "file", source }
            }));
        foreach (var dependency in CurrentModule.Dependencies.Joined())
        {
            if (dependency == null) continue;
            // Add compile commands for the dependency.
            IModuleInstancingParams createdParams = CurrentModule.Context.InstancingParams!.CreateCopyFor(dependency);
            CompilerBase? compiler = await CompilerRegistry.CreateInstanceFor(createdParams);
            if (compiler == null)
                continue;
            await compiler.Setup();
            await compiler.Generate("CompileCommandsJSON", outFile);
            var contents = await File.ReadAllTextAsync(Path.Join(CurrentModule.Context.ModuleDirectory?.FullName ?? "./", outFile ?? "compile_commands.json"));
            var dependencyJson = JsonSerializer.Deserialize<List<JsonObject>>(contents);
            if (dependencyJson != null)
            {
                jsonElements.AddRange(dependencyJson);
            }
        }

        var serialized = JsonSerializer.Serialize(jsonElements, CompileCommandsJsonSerializerOptions);
        await File.WriteAllTextAsync(
            Path.Join(CurrentModule.Context.ModuleDirectory?.FullName ?? "./", outFile),
            serialized);
        return true;
    }

    private static readonly JsonSerializerOptions CompileCommandsJsonSerializerOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public override LinkerBase GetDefaultLinker()
    {
        // Choose the appropriate MSVC linker based on module type
        if (CurrentModule?.Type == ModuleType.StaticLibrary)
        {
            return LinkerRegistry.GetInstance().Get<MsvcLibLinker>();
        }
        else
        {
            return LinkerRegistry.GetInstance().Get<MsvcLinkLinker>();
        }
    }

    public override bool IsAvailable(PlatformBase platform)
    {
        return platform.GetName() == "Win32";
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        throw new NotImplementedException();
    }
}
