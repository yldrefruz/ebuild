using System.Diagnostics;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Linker;

namespace ebuild.Linkers;



public class MsvcLibLinkerFactory : ILinkerFactory
{
    public string Name => "msvc.lib";

    public Type LinkerType => typeof(MsvcLibLinker);

    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return instancingParams.Platform.Name == "windows";
    }

    public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new MsvcLibLinker(instancingParams.Architecture);
    }
}


public class MsvcLibLinker : LinkerBase
{
    private static bool PathsInitialized = false;
    private string LibExecutablePath = "lib.exe";
    private string MsvcToolsLibPath = "";

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
        LibExecutablePath = Path.Join(MsvcToolsBinPath, "lib.exe");
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
    public MsvcLibLinker(Architecture targetArchitecture)
    {
        InitPaths(targetArchitecture);
    }

    public override async Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.OutputType != ModuleType.StaticLibrary)
        {
            throw new NotSupportedException("MSVC Lib does not support creating static libraries. Use the MSVC Linker instead.");
        }
        var arguments = new ArgumentBuilder();
        Directory.CreateDirectory(Path.GetDirectoryName(settings.OutputFile)!);
        arguments.Add("/NOLOGO");
        arguments.Add($"/OUT:{settings.OutputFile}");

        if (settings.IsDebugBuild)
        {
            arguments.Add("/DEBUG");
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

        var startInfo = new ProcessStartInfo
        {
            FileName = LibExecutablePath,
            Arguments = arguments.ToString(),
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
        return process.ExitCode == 0;
    }
}