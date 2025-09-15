using System.Diagnostics;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Linker;

namespace ebuild.Linkers
{
    public class ArLinkerFactory : ILinkerFactory
    {
        public string Name => "ar";

        public Type LinkerType => typeof(ArLinker);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            // AR is available on Unix-like platforms and only for static libraries
            return instancingParams.Platform.Name != "windows" && module.Type == ModuleType.StaticLibrary;
        }

        public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new ArLinker(instancingParams.Architecture);
        }
    }

    public class ArLinker : LinkerBase
    {
        private readonly Architecture _targetArchitecture;
        private readonly string _arExecutablePath;

        public ArLinker(Architecture targetArchitecture)
        {
            _targetArchitecture = targetArchitecture;
            _arExecutablePath = FindExecutable("ar") ?? throw new Exception("AR archiver not found in PATH");
        }

        private static string? FindExecutable(string executableName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
                return null;

            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, executableName);
                if (File.Exists(fullPath))
                    return fullPath;

                // Try with .exe extension on Windows (cross-compilation scenarios)
                var exePath = fullPath + ".exe";
                if (File.Exists(exePath))
                    return exePath;
            }
            return null;
        }

        public override async Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken = default)
        {
            if (settings.OutputType != ModuleType.StaticLibrary)
            {
                throw new NotSupportedException("AR archiver only supports creating static libraries. Use LD for executables and shared libraries.");
            }

            var arguments = new ArgumentBuilder();
            Directory.CreateDirectory(Path.GetDirectoryName(settings.OutputFile)!);

            // AR operation flags
            arguments.Add("rcs"); // r = insert files, c = create archive if it doesn't exist, s = write symbol table

            // Output archive file
            arguments.Add($"\"{settings.OutputFile}\"");

            // Convert .obj files to .o files for Unix compatibility
            var inputFiles = settings.InputFiles.Select(file => 
            {
                if (file.EndsWith(".obj"))
                {
                    var objFile = Path.ChangeExtension(file, ".o");
                    // If the .o file doesn't exist but .obj does, this suggests a naming mismatch
                    if (!File.Exists(objFile) && File.Exists(file))
                    {
                        Console.WriteLine($"Warning: Input file {file} uses Windows .obj extension but this is Unix. Looking for {objFile}");
                        return objFile; // Still use .o extension as that's what AR expects
                    }
                    return objFile;
                }
                return file;
            }).ToList();

            // Input object files
            arguments.AddRange(inputFiles);

            var startInfo = new ProcessStartInfo
            {
                FileName = _arExecutablePath,
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

            // Optionally run ranlib to generate or update the symbol table index
            if (process.ExitCode == 0)
            {
                var ranlibPath = FindExecutable("ranlib");
                if (!string.IsNullOrEmpty(ranlibPath))
                {
                    var ranlibStartInfo = new ProcessStartInfo
                    {
                        FileName = ranlibPath,
                        Arguments = $"\"{settings.OutputFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var ranlibProcess = new Process { StartInfo = ranlibStartInfo };
                    ranlibProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                    ranlibProcess.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);
                    ranlibProcess.Start();
                    ranlibProcess.BeginOutputReadLine();
                    ranlibProcess.BeginErrorReadLine();
                    await ranlibProcess.WaitForExitAsync(cancellationToken);
                    return ranlibProcess.ExitCode == 0;
                }
            }

            return process.ExitCode == 0;
        }
    }
}