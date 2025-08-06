using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

[Linker("MsvcLib")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class MsvcLibLinker : LinkerBase
{
    private string _msvcCompilerRoot = string.Empty;
    private string _msvcToolRoot = string.Empty;

    private static readonly ILogger Logger =
        EBuild.LoggerFactory.CreateLogger("MSVC Lib Linker");

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

        Logger.LogInformation("Linking module {moduleName} using lib.exe", CurrentModule.Name);

        try
        {
            switch (CurrentModule.Type)
            {
                case ModuleType.StaticLibrary:
                    return await CallLibExe();
                case ModuleType.SharedLibrary:
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    Logger.LogError("MsvcLibLinker can only link static libraries. Use MsvcLinkLinker for shared libraries and executables.");
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

    private async Task<bool> CallLibExe()
    {
        if (CurrentModule == null)
            return false;
            
        var libExe = Path.Join(GetMsvcCompilerBin(), "lib.exe");
        var files = Directory.GetFiles(GetObjectOutputFolder(), "*.obj", SearchOption.TopDirectoryOnly);
        //TODO: add files from the dependencies.
        Directory.CreateDirectory(GetBinaryOutputFolder());
        
        // ReSharper disable once StringLiteralTypo
        ArgumentBuilder argumentBuilder = new();
        argumentBuilder += "/nologo";

        if (CurrentModule.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase))
        {
            argumentBuilder += "/DEBUG";
            argumentBuilder += $"/PDB:\"{Path.Join(GetBinaryOutputFolder(), CurrentModule.Name ?? CurrentModule.GetType().Name)}.pdb\"";
        }

        argumentBuilder +=
            $"/OUT:\"{Path.Join(GetBinaryOutputFolder(), (CurrentModule.Name ?? CurrentModule.GetType().Name) + ".lib")}\"";
        argumentBuilder += AdditionalLinkerOptions;
        argumentBuilder += CurrentModule.LinkerOptions;
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
        var libExe = Path.Join(GetMsvcCompilerBin(), "lib.exe");
        if (libExe.Contains(' '))
        {
            libExe = "\"" + libExe + "\"";
        }
        return libExe;
    }
}