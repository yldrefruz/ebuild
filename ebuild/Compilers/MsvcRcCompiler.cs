using System.Diagnostics;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Compilers;

class MsvcRcCompilerFactory : ICompilerFactory
{
    public string Name => "msvc.rc";

    public Type CompilerType => typeof(MsvcRcCompiler);

    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return instancingParams.Platform.Name == "windows";
    }

    public CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new MsvcRcCompiler(instancingParams.Architecture);
    }

    public string GetExecutablePath(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        MsvcRcCompiler.InitPaths(instancingParams.Architecture);
        return MsvcRcCompiler.rcPath;
    }
}


class MsvcRcCompiler : CompilerBase
{
    internal static string rcPath = "rc.exe";
    internal static bool PathsInitialized = false;
    internal static void InitPaths(Architecture targetArchitecture)
    {
        if (PathsInitialized)
        {
            return;
        }
        var kit = MSVCUtils.GetWindowsKit(null) ?? throw new Exception("rc.exe couldn't be found due to missing windows sdk installation.");
        rcPath = Path.Join(kit.KitRoot, "bin", kit.Version, targetArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => throw new NotImplementedException()
        }, "rc.exe");
        PathsInitialized = true;
    }
    public MsvcRcCompiler(Architecture architecture)
    {
        InitPaths(architecture);
    }

    public async override Task<bool> Compile(CompilerSettings settings, CancellationToken cancellationToken)
    {
        ArgumentBuilder args = new();

        args.Add($"/fo{settings.OutputFile}");
        settings.IncludePaths.ForEach(v =>
        {
            args.Add("/i");
            args.Add(v);
        });
        settings.Definitions.ForEach(v =>
        {
            args.Add("/d");
            args.Add(v.ToString());
        });
        args.Add("/x");
        args.Add(settings.SourceFile);


        var startInfo = new ProcessStartInfo
        {
            FileName = rcPath,
            Arguments = args.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(settings.SourceFile) ?? Environment.CurrentDirectory
        };
        var process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        };
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Out.WriteLine(e.Data);
            }
        };
        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0;

    }

    public override Task<bool> Generate(CompilerSettings settings, CancellationToken cancellationToken, string type, object? data = null)
    {
        // No generation support for the rc files.
        return Task.FromResult(true);
    }
}