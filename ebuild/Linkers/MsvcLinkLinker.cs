using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

[Linker("MsvcLink")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class MsvcLinkLinker : LinkerBase
{
    private string _msvcCompilerRoot = string.Empty;
    private string _msvcToolRoot = string.Empty;

    private static readonly ILogger Logger =
        EBuild.LoggerFactory.CreateLogger("MSVC Link Linker");

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

        var toolRoot = await MSVCUtils.GetMsvcToolRoot();

        var version = await MSVCUtils.FindMsvcVersion(toolRoot, Logger);
        if (string.IsNullOrEmpty(version))
        {
            Logger.LogCritical("Couldn't find a valid msvc installation.");
            return false;
        }

        (_msvcToolRoot, _msvcCompilerRoot) = MSVCUtils.SetupMsvcPaths(toolRoot, version);
        return true;
    }

    public override async Task<bool> Link()
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for linking");
            return false;
        }

        Logger.LogInformation("Linking module {moduleName} using link.exe", CurrentModule.Name);

        try
        {
            switch (CurrentModule.Type)
            {
                case ModuleType.SharedLibrary:
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    return await CallLinkExe();
                case ModuleType.StaticLibrary:
                    Logger.LogError("MsvcLinkLinker cannot link static libraries. Use MsvcLibLinker instead.");
                    return false;
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
        argumentBuilder += CurrentModule.LinkerOptions;
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
                Logger.LogError("Static libraries should use MsvcLibLinker, not MsvcLinkLinker");
                return false;
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