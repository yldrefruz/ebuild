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

    private static string GetVsWhereDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Join(localAppData, "ebuild", "compilers", "msvc", "vswhere");
    }

    private bool VswhereExists()
    {
        var vsWhereHash = "C54F3B7C9164EA9A0DB8641E81ECDDA80C2664EF5A47C4191406F848CC07C662";
        var vsWhereExec = Path.Join(GetVsWhereDirectory(), "vswhere.exe");
        if (!File.Exists(vsWhereExec))
            return false;
        using var shaHasher = SHA256.Create();
        using var fs = File.Open(vsWhereExec, FileMode.Open);
        var hash = shaHasher.ComputeHash(fs);
        var stringBuilder = new StringBuilder();

        foreach (var b in hash)
            stringBuilder.AppendFormat("{0:X2}", b);
        var hashString = stringBuilder.ToString();
        return hashString == vsWhereHash;
    }

    private const string VsWhereUrl = "https://github.com/microsoft/vswhere/releases/download/3.1.7/vswhere.exe";

    string GetObjectOutputFolder()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");
        if (CurrentModule.UseVariants)
            return Path.Join(CurrentModule!.Context.ModuleDirectory!.FullName, ".ebuild", ((ModuleFile)CurrentModule.Context.SelfReference).Name, "build", CurrentModule.GetVariantId().ToString(), "obj") + Path.DirectorySeparatorChar;
        return Path.Join(CurrentModule!.Context.ModuleDirectory!.FullName, ".ebuild", ((ModuleFile)CurrentModule.Context.SelfReference).Name, "build", "obj") + Path.DirectorySeparatorChar;
    }
    string GetObjectPdbFolder() => GetObjectOutputFolder();

    string GetBinaryOutputFolder()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");
        return CurrentModule.GetBinaryOutputDirectory();
    }

    bool DownloadVsWhere()
    {
        HttpClient client = new HttpClient();
        byte[] vswhereBytes;
        try
        {
            var job = client.GetByteArrayAsync(VsWhereUrl);
            job.Wait();
            if (!job.IsCompletedSuccessfully)
                return false;
            vswhereBytes = job.Result;
        }
        catch (Exception)
        {
            return false;
        }

        var pathSegments = VsWhereUrl.Split("/");
        var version = pathSegments[pathSegments.Length - 1 - 1];
        var vswhereDirectory = GetVsWhereDirectory();
        Directory.CreateDirectory(vswhereDirectory);
        var versionFile = File.Create(Path.Join(vswhereDirectory, "VERSION"));
        TextWriter writer = new StreamWriter(versionFile, Encoding.UTF8);
        writer.Write(version);
        writer.Close();
        writer.Dispose();
        versionFile.Close();
        var vswhereFile = File.Create(Path.Join(vswhereDirectory, "vswhere.exe"));
        vswhereFile.Write(vswhereBytes);
        vswhereFile.Close();
        vswhereFile.Dispose();
        return true;
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
            args += $"/Fd\"{Path.Join(GetObjectPdbFolder(), CurrentModule.Name ?? CurrentModule.GetType().Name)}.pdb\"";
            args += "/FS";
            args += OptimizationLevelToArg(OptimizationLevel.None); // No optimization in debug
        }
        else
        {
            args += "/MD";
            args += OptimizationLevelToArg(CurrentModule.OptimizationLevel); // Use module's optimization level
        }
        args += $"/Fo:";
        Directory.CreateDirectory(GetObjectOutputFolder());
        args += GetObjectOutputFolder();

        if (ProcessCount != null)
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
        if (!VswhereExists())
        {
            if (!DownloadVsWhere())
            {
                throw new Exception(
                    $"Can't download vswhere from {VsWhereUrl}. Please check your internet connection.");
            }
        }

        var toolRoot = Config.Get().MsvcPath ?? string.Empty;
        if (string.IsNullOrEmpty(toolRoot))
        {
            var vsWhereExecutable = Path.Join(GetVsWhereDirectory(), "vswhere.exe");
            const string args =
                "-latest -products * -requires \"Microsoft.VisualStudio.Component.VC.CoreBuildTools\" -property installationPath";
            var vsWhereProcess = new Process();
            var processStartInfo = new ProcessStartInfo
            {
                Arguments = args,
                FileName = vsWhereExecutable,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            vsWhereProcess.StartInfo = processStartInfo;
            vsWhereProcess.Start();
            toolRoot = await vsWhereProcess.StandardOutput.ReadToEndAsync();
            await vsWhereProcess.WaitForExitAsync();
        }

        toolRoot = toolRoot.Trim();


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

        var commandFileContent = GenerateCompileCommand(true);
        //TODO: Remove this
        Logger.LogInformation(commandFileContent);
        var commandFilePath = Path.GetTempFileName();
        await using (var commandFile = File.OpenWrite(commandFilePath))
        {
            await using var writer = new StreamWriter(commandFile);
            await writer.WriteAsync(commandFileContent);
            await writer.FlushAsync();
            commandFile.Flush();
        }

        if (CleanCompilation)
        {
            //Delete all obj files before compiling
            ClearObjectAndPdbFiles(false);
        }

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

        if (File.Exists(commandFilePath))
            File.Delete(commandFilePath);
        if (proc.ExitCode != 0)
        {
            Logger.LogError("Compilation Failed, {exitCode}", proc.ExitCode);
            if (CleanCompilation)
            {
                ClearObjectAndPdbFiles();
            }

            return false;
        }

        Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName);
        switch (CurrentModule.Type)
        {
            case ModuleType.StaticLibrary:
                {
                    await CallLibExe();
                    break;
                }
            case ModuleType.SharedLibrary:
            case ModuleType.Executable:
            case ModuleType.ExecutableWin32:
                {
                    await CallLinkExe();
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }

        ProcessAdditionalDependencies();
        return true;
    }

    private void ClearObjectAndPdbFiles(bool shouldLog = true)
    {
        List<string> files =
        [
            .. Directory.GetFiles(GetObjectOutputFolder(), "*.obj", SearchOption.TopDirectoryOnly),
            .. Directory.GetFiles(GetObjectPdbFolder(), "*.pdb", SearchOption.TopDirectoryOnly),
        ];
        foreach (var file in files)
        {
            if (shouldLog)
                Logger.LogDebug("Compilation file {file} is being removed", file);
            try
            {
                File.Delete(Path.GetFullPath(file));
            }
            catch (Exception)
            {
                // ignored
            }
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

    private async Task CallLinkExe()
    {
        if (CurrentModule == null)
            return;
        Logger.LogInformation("Linking program");
        ArgumentBuilder argumentBuilder = new();
        var linkExe = Path.Join(GetMsvcCompilerBin(), "link.exe");
        var files = Directory.GetFiles(GetObjectOutputFolder(), "*.obj", SearchOption.TopDirectoryOnly);
        files = [.. files.Select(f => GetModuleFilePath(f, CurrentModule))];
        // ReSharper disable once StringLiteralTypo
        argumentBuilder += "/nologo";
        if (CurrentModule.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
        {
            argumentBuilder += "/DEBUG";
            // example: /PDB:"C:\Users\user\module_1\Binaries\<variant_id>\module_1.pdb"
            argumentBuilder += $"/PDB:{Path.Join(GetBinaryOutputFolder(), CurrentModule.Name ?? CurrentModule.GetType().Name)}.pdb";
        }

        argumentBuilder += AdditionalLinkerOptions;

        var outType = ".exe";
        switch (CurrentModule.Type)
        {
            case ModuleType.ExecutableWin32:
                argumentBuilder += "/SUBSYSTEM:WINDOWS";
                break;
            case ModuleType.Executable:
                argumentBuilder += "/SUBSYSTEM:CONSOLE";
                break;
            case ModuleType.SharedLibrary:
                argumentBuilder += "/DLL";
                outType = ".dll";
                break;
            case ModuleType.StaticLibrary:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        // Make sure the output directory exists.
        Directory.CreateDirectory(GetBinaryOutputFolder());
        argumentBuilder +=
            $"/OUT:\"{Path.Join(GetBinaryOutputFolder(),
               (CurrentModule.Name ?? CurrentModule.GetType().Name) + outType)}\"";

        // Add the library search paths for current module and the dependencies.
        argumentBuilder += CurrentModule.LibrarySearchPaths.Joined()
            .Select(current => $"/LIBPATH:\"{Path.GetFullPath(current)}\"");
        var depTree = ModuleFile.Get(CurrentModule.Context.ModuleFile.FullName).GetDependencyTree();
        foreach (var dependency in depTree.GetFirstLevelAndPublicDependencies())
        {
            argumentBuilder +=
                dependency.GetCompiledModule()!.LibrarySearchPaths.Public.Select(current =>
                    $"/LIBPATH:\"{Path.GetFullPath(current)}\"");
        }
        // Add the output files for current module.
        argumentBuilder += files;

        // Add the output file of the dependencies.
        foreach (var dependency in depTree.GetFirstLevelAndPublicDependencies())
        {
            var compModule = dependency.GetCompiledModule()!;
            switch (compModule.Type)
            {
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    // No need to add the executable files. And they shouldn't even be referenced.
                    throw new NotImplementedException("Executable modules are not supported as dependencies.");
                case ModuleType.SharedLibrary:
                    File.Copy(Path.Combine(compModule.GetBinaryOutputDirectory(), $"{compModule.Name}.dll"), Path.Combine(GetBinaryOutputFolder(), $"{CurrentModule.Name}.dll"), true); // copy the dll to the output directory.
                    argumentBuilder += Path.Combine(compModule.GetBinaryOutputDirectory(), $"{compModule.Name}.lib"); // link the library
                    break;
                case ModuleType.StaticLibrary:
                    argumentBuilder += Path.Combine(compModule.GetBinaryOutputDirectory(), $"{compModule.Name}.lib"); // link the library
                    break;
                default:
                    break;
            }
        }

        // Add the libraries for current module and the dependencies.
        argumentBuilder += CurrentModule.Libraries.Joined()
            .Select((a) => File.Exists(Path.GetFullPath(a)) ? Path.GetFullPath(a) : a);
        foreach (var dependency in depTree.GetFirstLevelAndPublicDependencies())
        {
            argumentBuilder += dependency.GetCompiledModule()!.Libraries.Public
                .Select((a) =>
                {
                    var shorterPath = GetModuleFilePath(a, dependency.GetCompiledModule()!);
                    return File.Exists(shorterPath) ? shorterPath : a;
                });
        }

        var tempFile = Path.GetTempFileName();
        var argumentString = argumentBuilder.ToString();
        await using (var commandFile = File.OpenWrite(tempFile))
        {
            await using var writer = new StreamWriter(commandFile);
            await writer.WriteAsync(argumentString);
        }

        using (Logger.BeginScope("Link"))
        {
            Logger.LogInformation("Launching link.exe with command file content {commandFileContent}", argumentString);
            var p = new ProcessStartInfo()
            {
                Arguments = $"@\"{tempFile}\"",
                FileName = linkExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = CurrentModule.Context.ModuleDirectory!.FullName,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            var process = new Process();
            process.StartInfo = p;
            process.OutputDataReceived += (_, args) =>
            {
                //TODO: change the parsing method. Maybe regex.
                if (args.Data == null) return;
                var splitData = args.Data.Split(":");
                if (splitData.Length <= 2) return;
                var errorWords = new[] { "error", "fatal error" };
                var warningWords = new[] { "warning" };
                if (errorWords.Any(word => splitData[1].Trim().StartsWith(word)))
                {
                    Logger.LogError("{data}", args.Data);
                    return;
                }

                if (warningWords.Any(word => splitData[1].Trim().StartsWith(word)))
                {
                    Logger.LogWarning("{data}", args.Data);
                    return;
                }

                Logger.LogInformation("{data}", args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null) Logger.LogError("{data}", args.Data);
            };
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Logger.LogError("link failed, exit code: {exitCode}", process.ExitCode);
                Directory.SetCurrentDirectory(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent!.FullName);
                return;
            }

            Directory.SetCurrentDirectory(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent!.FullName);
        }
    }

    private async Task CallLibExe()
    {
        if (CurrentModule == null)
            return;
        var libExe = Path.Join(GetMsvcCompilerBin(), "lib.exe");
        var files = Directory.GetFiles(GetObjectOutputFolder(), "*.obj", SearchOption.TopDirectoryOnly);
        //TODO: add files from the dependencies.
        Directory.CreateDirectory(Path.Join(GetBinaryOutputFolder(), "lib"));
        // ReSharper disable once StringLiteralTypo
        ArgumentBuilder argumentBuilder = new();
        argumentBuilder += "/nologo";
        if (CurrentModule.Type == ModuleType.SharedLibrary)
        {
            argumentBuilder += "/DLL";
        }

        if (CurrentModule.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
        {
            argumentBuilder += "/DEBUG";
            argumentBuilder += $"/PDB:\"{Path.Join(GetBinaryOutputFolder(), CurrentModule.Name ?? CurrentModule.GetType().Name)}.pdb\"";
        }

        argumentBuilder +=
            $"/OUT:\"{Path.Join(GetBinaryOutputFolder(), "lib", (CurrentModule.Name ?? CurrentModule.GetType().Name) + ".lib")}\"";
        argumentBuilder += AdditionalLinkerOptions;
        argumentBuilder += CurrentModule.LibrarySearchPaths.Joined().Select(s => $"/LIBPATH:\"{s}\"");
        argumentBuilder += files.Select(f => GetModuleFilePath(f, CurrentModule));
        argumentBuilder += CurrentModule.Libraries.Joined()
            .Select(s => File.Exists(Path.GetFullPath(s)) ? Path.GetFullPath(s) : s);

        var tempFile = Path.GetTempFileName();
        var argumentContents = argumentBuilder.ToString();
        await using (var commandFile = File.OpenWrite(tempFile))
        {
            await using var writer = new StreamWriter(commandFile);
            await writer.WriteAsync(argumentContents);
        }

        using (Logger.BeginScope("Lib"))
        {
            Logger.LogDebug("Launching lib.exe with command file content {libCommandFileContent}",
                argumentContents);
            var p = new ProcessStartInfo()
            {
                Arguments = $"@\"{tempFile}\"",
                FileName = libExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = CurrentModule.Context.ModuleDirectory!.FullName,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            var process = new Process();
            process.StartInfo = p;
            process.OutputDataReceived += (_, args) => Logger.LogInformation("{data}", args.Data);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                Logger.LogError("LIB.exe failed, exit code: {exitCode}", process.ExitCode);
                Environment.ExitCode = -1;
                return;
            }
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

    public override bool IsAvailable(PlatformBase platform)
    {
        return platform.GetName() == "Win32";
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        throw new NotImplementedException();
    }
}