using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Linker;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

public class MsvcLinkLinkerFactory : ILinkerFactory
{
    public string Name => "msvc.link";

    public Type LinkerType => typeof(MsvcLinkLinker);

    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return instancingParams.Platform.Name == "windows";
    }

    public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new MsvcLinkLinker(instancingParams.Architecture);
    }
}

public class MsvcLinkLinker : LinkerBase
{
    public MsvcLinkLinker(Architecture targetArchitecture)
    {
        InitPaths(targetArchitecture);
    }
    private static bool PathsInitialized = false;
    private static ILogger Logger = EBuild.LoggerFactory.CreateLogger<MsvcLinkLinker>();

    private void InitPaths(Architecture targetArchitecture)
    {
        if (PathsInitialized)
        {
            return;
        }
        if (!MSVCUtils.VswhereExists())
        {
            if (!MSVCUtils.DownloadVsWhere())
            {
                throw new Exception(
                    $"Can't download vswhere from {MSVCUtils.VsWhereUrl}. Please check your internet connection.");
            }
        }

        var toolRoot = MSVCUtils.GetMsvcToolRoot().Result;

        var version = MSVCUtils.FindMsvcVersion(toolRoot).Result;
        if (string.IsNullOrEmpty(version))
        {
            throw new Exception("Couldn't find a valid msvc installation.");
        }

        var (MsvcToolRoot, MsvcToolsBinRoot) = MSVCUtils.SetupMsvcPaths(toolRoot, version);
        var MsvcToolsBinPath = Path.Join(MsvcToolsBinRoot, targetArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x86"
        });
        LinkExecutablePath = Path.Join(MsvcToolsBinPath, "link.exe");
        MsvcToolsLibPath = Path.Join(MsvcToolRoot, "lib", targetArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x86"
        });
        PathsInitialized = true;
    }
    private static string LinkExecutablePath = "link.exe";
    private static string MsvcToolsLibPath = string.Empty;

    public override async Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.OutputType == ModuleType.StaticLibrary)
        {
            throw new NotSupportedException("MSVC Linker does not support creating static libraries. Use the MSVC Lib instead.");
        }
        var arguments = new ArgumentBuilder();
        Directory.CreateDirectory(Path.GetDirectoryName(settings.OutputFile)!);
        arguments.Add("/NOLOGO");
        arguments.Add($"/OUT:{settings.OutputFile}");
        if (settings.OutputType == ModuleType.ExecutableWin32)
        {
            arguments.Add("/SUBSYSTEM:WINDOWS");
        }
        else if (settings.OutputType == ModuleType.Executable)
        {
            arguments.Add("/SUBSYSTEM:CONSOLE");
        }

        if (settings.IsDebugBuild)
        {
            arguments.Add("/DEBUG:FULL");
            arguments.Add("/INCREMENTAL");
            arguments.Add($"/ILK:\"{settings.IntermediateDir}/{Path.GetFileNameWithoutExtension(settings.OutputFile) + ".ilk"}\"");
        }
        else
        {
            arguments.Add("/LTCG");
            arguments.Add("/INCREMENTAL:NO");
            arguments.Add("/OPT:REF");
        }

        arguments.AddRange(settings.DelayLoadLibraries.Select(v => $"/DELAYLOAD:{v}"));
        if (settings.ShouldCreateDebugFiles)
        {
            var debugFile = Path.ChangeExtension(settings.OutputFile, ".pdb");
            arguments.Add($"/PDB:{debugFile}");
            arguments.Add($"/MAP:{Path.ChangeExtension(settings.OutputFile, ".map")}");
            arguments.Add("/MAPINFO:EXPORTS");
        }
        if (settings.OutputType == ModuleType.SharedLibrary)
        {
            arguments.Add("/DLL");
        }

        arguments.AddRange(settings.LibraryPaths.Select(v => $"/LIBPATH:{v}"));
        arguments.Add($"/LIBPATH:{MsvcToolsLibPath}");

        arguments.Add($"/MACHINE:{settings.TargetArchitecture switch
        {
            Architecture.X64 => "X64",
            Architecture.X86 => "X86",
            Architecture.Arm64 => "ARM64",
            Architecture.Arm => "ARM",
            _ => "X86"
        }}");

        arguments.AddRange(settings.LinkerFlags);
        arguments.AddRange(settings.InputFiles);

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, arguments.ToString());
        Logger.LogDebug("Linker command: {Linker} @{ResponseFile}", LinkExecutablePath, tempFile);
        Logger.LogDebug("Linker arguments: {Arguments}", arguments.ToString());
        var startInfo = new ProcessStartInfo
        {
            FileName = LinkExecutablePath,
            Arguments = "@\"" + tempFile + "\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var process = new Process
        {
            StartInfo = startInfo
        };
        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        try { File.Delete(tempFile); } catch { /* ignore errors from deleting temp file */ }
        return process.ExitCode == 0;
    }
}