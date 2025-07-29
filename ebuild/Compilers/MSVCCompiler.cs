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

        CurrentModule.Includes.Private.AddRange(new[]
        {
            Path.Join(_msvcToolRoot, "include"),
            //TODO: programatically find this.
            @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt",
            @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um",
            @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\shared",
            @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\winrt"
        });

        CurrentModule.LibrarySearchPaths.Private.AddRange(new[]
        {
            @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\um\x64",
            @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt\x64",
            @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt_enclave\x64",
            GetMsvcCompilerLib()
        });
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

    private void AddModuleCompileArguments(ModuleBase module, bool includeSourceFiles, ref ArgumentBuilder args,
        AccessLimit? accessLimit = null)
    {
        args += module.Definitions.GetLimited(accessLimit).Select(definition => $"/D\"{definition}\"");

        args += module.Includes.GetLimited(accessLimit).Select(include => $"/I\"{GetModuleFilePath(include, module)}\"");
        args += module.ForceIncludes.GetLimited(accessLimit).Select(s => $"/FI{GetModuleFilePath(s, module)}");

        if (includeSourceFiles)
        {
            args += $"/D\"{(module.Name ?? module.Context.ModuleDirectory!.Name).ToUpperInvariant()}_BUILDING\"";
            args += module.SourceFiles.Select(s => GetModuleFilePath(s, module));
        }
    }

    private string GenerateCompileCommand(bool bSourceFiles)
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
        if (bSourceFiles)
        {
            args += $"/Fo:";
            Directory.CreateDirectory(CompilerUtils.GetObjectOutputFolder(CurrentModule));
            args += CompilerUtils.GetObjectOutputFolder(CurrentModule);
        }

        if (ProcessCount != null && bSourceFiles)
        {
            args += $"/MP{(ProcessCount <= 0 ? string.Empty : ProcessCount.ToString())}";
        }



        args += AdditionalCompilerOptions;

        AddModuleCompileArguments(CurrentModule, bSourceFiles, ref args);


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


        var version = Config.Get().MsvcVersion ?? string.Empty;
        version = version.Trim();
        if (!File.Exists(Path.Join(toolRoot, "VC", "Tools", "MSVC", version)))
        {
            Logger.LogInformation("(Config) => Msvc Version: {version} is not found, trying to find a valid version.",
                string.IsNullOrEmpty(version) ? version : "<Empty>");
        }

        if (string.IsNullOrEmpty(version))
        {
            Dictionary<Version, string> versionDict = new();
            foreach (var file in Directory.GetFiles(Path.Join(toolRoot, "VC",
                         "Auxiliary", "Build"), "Microsoft.VCToolsVersion.*default.txt"))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    if (Version.TryParse(content, out var foundVer))
                    {
                        versionDict.Add(foundVer, content);
                        using (Logger.BeginScope("Version Discovery"))
                        {
                            Logger.LogInformation("Found version: {content}", content);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to read version file: {file}", file);
                }
            }

            var latestVer = versionDict.Keys.ToList().OrderDescending().FirstOrDefault();
            if (latestVer != null) version = versionDict[latestVer];
        }

        version = version.Trim();
        if (string.IsNullOrEmpty(version))
        {
            Logger.LogCritical("Couldn't find a valid msvc installation.");
            return false;
        }

        _msvcToolRoot = Path.Join(toolRoot, "VC", "Tools", "MSVC", version);
        var host = "Hostx86";
        if (Environment.Is64BitOperatingSystem)
        {
            host = "Hostx64";
        }

        _msvcCompilerRoot = Path.Join(_msvcToolRoot, "bin", host);
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
        Directory.CreateDirectory(outputDir);

        var errorFiles = new List<string>();

        // Compile each source file individually
        foreach (var sourceFile in sourceFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            var objectFile = Path.Combine(outputDir, $"{fileName}.obj");

            // Generate compile command for this specific source file
            var commandContent = GenerateCompileCommand(false); // Don't include all source files
            commandContent += $" \"{GetModuleFilePath(sourceFile, CurrentModule)}\""; // Add this specific source file
            commandContent += $" /Fo\"{objectFile}\""; // Add specific output file

            // Write command to a temporary file to avoid command length limits
            var commandFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(commandFilePath, commandContent);

            var startInfo = new ProcessStartInfo()
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                Arguments = $"@\"{commandFilePath}\"",
                FileName = GetExecutablePath(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            Logger.LogInformation("Compiling: {sourceFile}", sourceFile);
            Logger.LogDebug("Command file content: {content}", commandContent);

            var proc = Process.Start(startInfo);
            if (proc == null)
            {
                Logger.LogError("Can't start cl.exe");
                Environment.ExitCode = 1;
                return false;
            }

            proc.OutputDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    var match = CLMessageRegex.Match(args.Data);
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
                    else if (type == "note")
                    {
                        Logger.LogWarning("{file}({location}): {type}: {message}", file, location, type, message);
                    }
                    else
                    {
                        Logger.LogInformation("{data}", args.Data);
                    }
                }
            };
            proc.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null) Logger.LogError("{data}", args.Data);
            };
            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            await proc.WaitForExitAsync();

            // Clean up command file
            if (File.Exists(commandFilePath))
            {
                try
                {
                    File.Delete(commandFilePath);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Failed to delete command file {file}: {error}", commandFilePath, ex.Message);
                }
            }

            if (proc.ExitCode != 0)
            {
                Logger.LogError("Compilation Failed for {sourceFile}, {exitCode}", sourceFile, proc.ExitCode);
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

        var jsonArr =
            CurrentModule.SourceFiles.Select(source => new JsonObject
            {
                { "directory", Directory.GetCurrentDirectory() },
                { "command", GetExecutablePath() + " " + command + " " + $"\"{source}\"" },
                { "file", source }
            });
        var serialized = JsonSerializer.Serialize(jsonArr, CompileCommandsJsonSerializerOptions);
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
        return LinkerRegistry.GetInstance().Get<MsvcLinkLinker>();
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
