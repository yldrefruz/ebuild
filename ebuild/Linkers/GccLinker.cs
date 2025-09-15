using System.Diagnostics;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Linker;

namespace ebuild.Linkers
{
    public class GccLinkerFactory : ILinkerFactory
    {
        public string Name => "gcc.linker";

        public Type LinkerType => typeof(GccLinker);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            // GCC linker is available on Unix-like platforms and for non-static libraries
            return instancingParams.Platform.Name != "windows" && module.Type != ModuleType.StaticLibrary;
        }

        public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new GccLinker(instancingParams.Architecture);
        }
    }

    public class GccLinker : LinkerBase
    {
        private readonly Architecture _targetArchitecture;
        private readonly string _gccExecutablePath;

        public GccLinker(Architecture targetArchitecture)
        {
            _targetArchitecture = targetArchitecture;
            _gccExecutablePath = FindExecutable("gcc") ?? throw new Exception("GCC compiler not found in PATH");
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
            if (settings.OutputType == ModuleType.StaticLibrary)
            {
                throw new NotSupportedException("GCC linker does not support creating static libraries. Use AR instead.");
            }

            var arguments = new ArgumentBuilder();
            var outputDir = Path.GetDirectoryName(settings.OutputFile);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Output file
            arguments.Add("-o");
            arguments.Add(settings.OutputFile);

            // Architecture-specific flags
            if (_targetArchitecture == Architecture.X86)
            {
                arguments.Add("-m32");
            }
            else if (_targetArchitecture == Architecture.X64)
            {
                arguments.Add("-m64");
            }

            // Shared library creation
            if (settings.OutputType == ModuleType.SharedLibrary)
            {
                arguments.Add("-shared");
                arguments.Add("-fPIC");
            }

            // Debug information
            if (settings.ShouldCreateDebugFiles && settings.IsDebugBuild)
            {
                arguments.Add("-g");
            }
            else if (!settings.IsDebugBuild)
            {
                arguments.Add("-s"); // Strip symbols for release builds
            }

            // Library paths
            foreach (var libPath in settings.LibraryPaths)
            {
                arguments.Add("-L");
                arguments.Add(libPath);
            }

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
                        return objFile; // Still use .o extension as that's what GCC expects
                    }
                    return objFile;
                }
                return file;
            }).ToList();

            // Input object files
            arguments.AddRange(inputFiles);

            // Additional linker flags
            arguments.AddRange(settings.LinkerFlags);

            var startInfo = new ProcessStartInfo
            {
                FileName = _gccExecutablePath,
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
}