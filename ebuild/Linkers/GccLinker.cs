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
            // GCC linker is available if gcc exists on PATH and for non-static libraries
            return FindExecutable("gcc") != null && module.Type != ModuleType.StaticLibrary;
        }

        public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new GccLinker(instancingParams.Architecture, module.CStandard == null);
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
    }

    public class GccLinker : LinkerBase
    {
        private readonly Architecture _targetArchitecture;
        private readonly string _gccExecutablePath;
        private readonly string _gxxExecutablePath;
        private readonly bool _useGxx;

        public GccLinker(Architecture targetArchitecture, bool useGxx = true)
        {
            _targetArchitecture = targetArchitecture;
            _gccExecutablePath = FindExecutable("gcc") ?? throw new Exception("GCC compiler not found in PATH");
            _gxxExecutablePath = FindExecutable("g++") ?? throw new Exception("G++ compiler not found in PATH");
            _useGxx = useGxx;
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
            else if (_targetArchitecture == Architecture.Arm)
            {
                arguments.Add("-march=armv7-a");
            }
            else if (_targetArchitecture == Architecture.Arm64)
            {
                arguments.Add("-march=armv8-a");
            }
            else if (_targetArchitecture == Architecture.Armv6)
            {
                arguments.Add("-march=armv6");
            }
            else if (_targetArchitecture == Architecture.Wasm)
            {
                // WebAssembly target would need special handling
                arguments.Add("-target");
                arguments.Add("wasm32");
            }
            else if (_targetArchitecture == Architecture.S390x)
            {
                arguments.Add("-march=z196");
            }
            else if (_targetArchitecture == Architecture.LoongArch64)
            {
                arguments.Add("-march=loongarch64");
            }
            else if (_targetArchitecture == Architecture.Ppc64le)
            {
                arguments.Add("-mcpu=power8");
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

            // Input object files - use them as provided by the platform
            arguments.AddRange(settings.InputFiles);

            // Additional linker flags
            arguments.AddRange(settings.LinkerFlags);

            Console.WriteLine($"Running Linker: {(_useGxx ? _gxxExecutablePath : _gccExecutablePath)} {arguments}");

            var startInfo = new ProcessStartInfo
            {
                FileName = _useGxx ? _gxxExecutablePath : _gccExecutablePath,
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