using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

[Linker("Msvc")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class MsvcLinker : LinkerBase
{
    private string _msvcCompilerRoot = string.Empty;
    private string _msvcToolRoot = string.Empty;

    private static readonly ILogger Logger =
        LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = true;
            }))
            .CreateLogger("MSVC Linker");

    public override bool IsAvailable(PlatformBase platform)
    {
        return platform.GetName() == "Win32";
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

        var toolRoot = Config.Get().MsvcPath ?? string.Empty;
        if (string.IsNullOrEmpty(toolRoot))
        {
            var vsWhereExecutable = Path.Join(MSVCUtils.GetVsWhereDirectory(), "vswhere.exe");
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

    public override async Task<bool> Link()
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for linking");
            return false;
        }

        Logger.LogInformation("Linking module {moduleName}", CurrentModule.Name);

        try
        {
            switch (CurrentModule.Type)
            {
                case ModuleType.StaticLibrary:
                    return await CallLibExe();
                case ModuleType.SharedLibrary:
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    return await CallLinkExe();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Linking failed with exception: {message}", ex.Message);
            return false;
        }
    }

    private async Task<bool> CallLinkExe()
    {
        if (CurrentModule == null)
            return false;
        
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
                return false;
            }

            return true;
        }
    }

    private async Task<bool> CallLibExe()
    {
        if (CurrentModule == null)
            return false;
            
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
                return false;
            }
        }

        return true;
    }

    private string GetMsvcCompilerBin()
    {
        var targetArch = "x86";
        if (CurrentModule is { Context.TargetArchitecture: Architecture.X64 })
            targetArch = "x64";
        var msvcCompilerBin = Path.Join(_msvcCompilerRoot, targetArch);
        return msvcCompilerBin;
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
        var linkExe = Path.Join(GetMsvcCompilerBin(), "link.exe");
        if (linkExe.Contains(' '))
        {
            linkExe = "\"" + linkExe + "\"";
        }
        return linkExe;
    }
}