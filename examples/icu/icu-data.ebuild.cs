namespace thirdparty.icu;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ebuild.api;


public class IcuData : ModuleBase
{
    Process RunAndWait(string name, ProcessStartInfo psi)
    {
        return RunAndWait(name, psi, out _, out _);
    }
    Process RunAndWait(string name, ProcessStartInfo psi, out string standardOut)
    {
        return RunAndWait(name, psi, out standardOut, out _);
    }

    Process RunAndWait(string name, ProcessStartInfo psi, out string standardOut, out string standardError)
    {
        var process = Process.Start(psi) ?? throw new Exception($"Failed to start process {psi.FileName} {psi.Arguments}.");
        var _out = string.Empty;
        var _err = string.Empty;
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                _out += e.Data + Environment.NewLine;
                Console.WriteLine($"[{name}] info: {e.Data}");
            }
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                _err += e.Data + Environment.NewLine;
                Console.Error.WriteLine($"[{name}] error: {e.Data}");
            }
        };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        process.WaitForExit();
        standardOut = _out;
        standardError = _err;
        if (process.ExitCode != 0)
        {
            throw new Exception($"[{name}] Process failed with exit code {process.ExitCode}. Error: {standardError}");
        }
        return process;
    }

    public enum DataPackageType
    {
        Common,
        Static,
        Shared
    }

    DataPackageType PackageType = DataPackageType.Common;

    [ModuleOption(ChangesResultBinary = false, Description = "Set maximum number of parallel processes when compiling dependencies. Default is number of processors.")]
    int MaxDependencyCompilationProcesses = Environment.ProcessorCount;
    public IcuData(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.LibraryLoader;
        this.Name = "icudt";
        this.OutputDirectory = "Binaries/icudt";
        this.UseVariants = false;

        Directory.CreateDirectory(GetBinaryOutputDirectory());

        PackageType = context.SelfReference.GetOutput() switch
        {
            "static" => DataPackageType.Static,
            "shared" => DataPackageType.Shared,
            "common" => DataPackageType.Common,
            _ => throw new Exception("Invalid output type for IcuData module. Must be either static, shared or common."),
        };
        var toLinkFile = string.Empty;
        if (PackageType == DataPackageType.Shared || PackageType == DataPackageType.Static)
        {
            if (context.Platform.Name == "windows")
            {
                toLinkFile = Path.Join(context.ModuleDirectory.FullName, "Binaries", "icudt", "default", (PackageType == DataPackageType.Static ? "s" : string.Empty) + "icudt77" + context.Platform.ExtensionForStaticLibrary);
            }
            else if (context.Platform.Name == "unix")
            {
                toLinkFile = Path.Join(context.ModuleDirectory.FullName, "Binaries", "icudt", "default", "libicudt77" + context.Platform.ExtensionForStaticLibrary);
            }
            this.Libraries.Public.Add(toLinkFile);
        }
        if (File.Exists(toLinkFile))
        {
            // already built, nothing to do.
            return;
        }
        var ebuildPath = typeof(ModuleBase).Assembly.Location.Replace("ebuild.api.dll", "ebuild.dll");
        // If these are not added to prebuild steps like this. The generation or other steps might take too much time.
        this.PreBuildSteps.Add(new ModuleBuildStep("Build icupkg", (workerType, cancellationToken) =>
        {
            if (!File.Exists(Path.Join(context.ModuleDirectory.FullName, "Binaries", "icupkg", "default", "icupkg" + context.Platform.ExtensionForExecutable)))
            {
                Console.WriteLine("icupkg not found, building icupkg first.");
                var processStartArgs = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{ebuildPath}\" build {Path.Join(context.ModuleDirectory.FullName, "icu-icupkg.ebuild.cs")} -p {MaxDependencyCompilationProcesses}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var process = RunAndWait("compileIcuPkg", processStartArgs);
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Ebuild process for icu-icupkg failed with exit code {process.ExitCode}.");
                }
            }
            return Task.CompletedTask;
        }));
        this.PreBuildSteps.Add(new ModuleBuildStep("Run icupkg", (workerType, cancellationToken) =>
        {
            // unpack data if not already unpacked and list file output is not there..
            if (!Directory.Exists(Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents")) || !File.Exists(Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents.lst")))
            {
                Console.WriteLine("Unpacking icu data package for processing.");
                Directory.CreateDirectory(Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents"));
                var processStartArgs = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.Join(context.ModuleDirectory.FullName, "Binaries", "icupkg", "default", "icupkg" + context.Platform.ExtensionForExecutable),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                string[] argumentList = [
                        "-d",
                    Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents"),
                    "-x",
                    "*",
                    "-l",
                    Path.Join(context.ModuleDirectory.FullName, "data", $"{(BitConverter.IsLittleEndian ? "little" : "big")}", $"icudt77{(BitConverter.IsLittleEndian ? "l" : "b")}.dat")
                    ];
                foreach (var arg in argumentList)
                {
                    processStartArgs.ArgumentList.Add(arg);
                }
                var process = RunAndWait("icupkg", processStartArgs, out var standardOut, out var standardError);
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Unpacking icu data package completed successfully.");
                }

                Console.WriteLine("writing pack_contents.lst");
                File.WriteAllText(Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents.lst"), standardOut);
            }
            else
            {
                Console.WriteLine("icu data already unpacked, skipping unpacking.");
            }
            return Task.CompletedTask;
        }));

        this.PreBuildSteps.Add(new ModuleBuildStep("Build pkgdata", (workerType, cancellationToken) =>
        {
            // pkgdata
            if (!File.Exists(Path.Join(context.ModuleDirectory.FullName, "Binaries", "pkgdata", "default", "pkgdata" + context.Platform.ExtensionForExecutable)))
            {
                Console.WriteLine("pkgdata not found, building pkgdata first.");
                var processStartArgs = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{ebuildPath}\" build {Path.Join(context.ModuleDirectory.FullName, "icu-pkgdata.ebuild.cs")} -p {MaxDependencyCompilationProcesses}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                var process = RunAndWait("compilePkgData", processStartArgs);
            }
            return Task.CompletedTask;
        }));

        this.PreBuildSteps.Add(new ModuleBuildStep("run pkgdata", (workerType, cancellationToken) =>
        {
            if (!File.Exists(toLinkFile)) // if the file to link does not exist, we need to build it.
            {
                Directory.CreateDirectory(Path.Join(context.ModuleDirectory.FullName, "temp", "packaging_temp"));
                // provide all the env vars for the required tools
                Console.WriteLine("icu data library not found, building icu data library.");
                var processStartArgs = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.Join(context.ModuleDirectory.FullName, "Binaries", "pkgdata", "default", "pkgdata" + context.Platform.ExtensionForExecutable),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                List<string> argumentList = [
                    "-c",
                    "-m",
                    PackageType switch
                    {
                        DataPackageType.Common => "common",
                        DataPackageType.Static => "static",
                        DataPackageType.Shared => "library",
                        _ => throw new Exception("Invalid package type."),
                    },
                    "-v",
                    "-d",
                    Path.Join(context.ModuleDirectory.FullName, "Binaries","icudt", "default"),
                    "-s",
                    Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents"),
                    "-p",
                    "icudt77",
                    "-T",
                    Path.Join(context.ModuleDirectory.FullName, "temp", "packaging_temp"),
                    "-L",
                    "icudt77",
                ];
                if (context.Platform.Name == "windows" && context.RequestedOutput == "shared")
                {
                    argumentList.Add("-a");
                    argumentList.Add(context.TargetArchitecture switch
                    {
                        Architecture.X64 => "x64",
                        Architecture.X86 => "x86",
                        Architecture.Arm64 => "arm64",
                        Architecture.Arm => "arm",
                        _ => throw new Exception("Invalid architecture.")
                    });
                }
                argumentList.Add(Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents.lst"));
                foreach (var arg in argumentList)
                {
                    processStartArgs.ArgumentList.Add(arg);
                }
                var compilerFactory = (context.Toolchain.GetCompilerFactory(this, context.InstancingParams ?? throw new Exception("InstancingParams is null")) ?? throw new Exception("CompilerFactory is null"))!;
                var targetPath = Path.GetDirectoryName(compilerFactory.GetExecutablePath(this, context.InstancingParams ?? throw new Exception("InstancingParams is null")));
                processStartArgs.Environment["PATH"] = targetPath + ";" + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);
                Console.WriteLine("Using PATH: " + processStartArgs.Environment["PATH"]);

                var process = RunAndWait("pkgdata", processStartArgs);
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Packaging icu data library completed successfully.");
                }
                // Copy the output to the binaries directory.
                var libFile = Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents", "icudt77" + context.Platform.ExtensionForStaticLibrary);
                if (File.Exists(libFile))
                {
                    File.Copy(libFile, Path.Join(GetBinaryOutputDirectory(), "icudt77" + context.Platform.ExtensionForStaticLibrary), true);
                    File.Delete(libFile);
                }
                var dllFile = Path.Join(context.ModuleDirectory.FullName, "temp", "pack_contents", "icudt77" + context.Platform.ExtensionForSharedLibrary);
                if (File.Exists(dllFile))
                {
                    File.Copy(dllFile, Path.Join(GetBinaryOutputDirectory(), "icudt77" + context.Platform.ExtensionForSharedLibrary), true);
                    File.Delete(dllFile);
                }
            }
            return Task.CompletedTask;
        }));






    }
}