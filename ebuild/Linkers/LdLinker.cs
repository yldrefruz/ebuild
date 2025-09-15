using System.Diagnostics;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Linker;

namespace ebuild.Linkers
{
    public class LdLinkerFactory : ILinkerFactory
    {
        public string Name => "ld";

        public Type LinkerType => typeof(LdLinker);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            // LD is available on Unix-like platforms and for non-static libraries
            return instancingParams.Platform.Name != "windows" && module.Type != ModuleType.StaticLibrary;
        }

        public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new LdLinker(instancingParams.Architecture);
        }
    }

    public class LdLinker : LinkerBase
    {
        private readonly Architecture _targetArchitecture;
        private readonly string _ldExecutablePath;

        public LdLinker(Architecture targetArchitecture)
        {
            _targetArchitecture = targetArchitecture;
            _ldExecutablePath = FindExecutable("ld") ?? throw new Exception("LD linker not found in PATH");
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
                throw new NotSupportedException("LD linker does not support creating static libraries. Use AR instead.");
            }

            var arguments = new ArgumentBuilder();
            Directory.CreateDirectory(Path.GetDirectoryName(settings.OutputFile)!);

            // Output file
            arguments.Add($"-o \"{settings.OutputFile}\"");

            // Architecture-specific flags
            if (_targetArchitecture == Architecture.X86)
            {
                arguments.Add("-m elf_i386");
            }
            else if (_targetArchitecture == Architecture.X64)
            {
                arguments.Add("-m elf_x86_64");
            }
            else if (_targetArchitecture == Architecture.Arm)
            {
                arguments.Add("-m armelf_linux_eabi");
            }
            else if (_targetArchitecture == Architecture.Arm64)
            {
                arguments.Add("-m aarch64linux");
            }

            // Shared library creation
            if (settings.OutputType == ModuleType.SharedLibrary)
            {
                arguments.Add("-shared");
            }

            // Dynamic linker (for executables)
            if (settings.OutputType == ModuleType.Executable || settings.OutputType == ModuleType.ExecutableWin32)
            {
                // Add dynamic linker for the target architecture
                if (_targetArchitecture == Architecture.X64)
                {
                    arguments.Add("-dynamic-linker /lib64/ld-linux-x86-64.so.2");
                }
                else if (_targetArchitecture == Architecture.X86)
                {
                    arguments.Add("-dynamic-linker /lib/ld-linux.so.2");
                }
                else if (_targetArchitecture == Architecture.Arm64)
                {
                    arguments.Add("-dynamic-linker /lib/ld-linux-aarch64.so.1");
                }
                else if (_targetArchitecture == Architecture.Arm)
                {
                    arguments.Add("-dynamic-linker /lib/ld-linux-armhf.so.3");
                }
            }

            // Debug information
            if (settings.ShouldCreateDebugFiles && settings.IsDebugBuild)
            {
                // Keep debug sections
                arguments.Add("-g");
            }
            else if (!settings.IsDebugBuild)
            {
                // Strip symbols for release builds
                arguments.Add("-s");
            }

            // Library paths
            foreach (var libPath in settings.LibraryPaths)
            {
                arguments.Add($"-L\"{libPath}\"");
            }

            // Add standard library paths
            arguments.Add("-L/usr/lib");
            arguments.Add("-L/lib");
            if (_targetArchitecture == Architecture.X64)
            {
                arguments.Add("-L/usr/lib/x86_64-linux-gnu");
                arguments.Add("-L/lib/x86_64-linux-gnu");
            }
            else if (_targetArchitecture == Architecture.X86)
            {
                arguments.Add("-L/usr/lib/i386-linux-gnu");
                arguments.Add("-L/lib/i386-linux-gnu");
            }

            // Input object files
            arguments.AddRange(settings.InputFiles);

            // Standard startup files for executables
            if (settings.OutputType == ModuleType.Executable || settings.OutputType == ModuleType.ExecutableWin32)
            {
                // Add crt1.o, crti.o at the beginning and crtn.o at the end
                var crtPath = GetCrtPath();
                if (!string.IsNullOrEmpty(crtPath))
                {
                    arguments.Add($"\"{Path.Combine(crtPath, "crt1.o")}\"");
                    arguments.Add($"\"{Path.Combine(crtPath, "crti.o")}\"");
                }
            }

            // Standard libraries (typically linked last)
            arguments.Add("-lc"); // Standard C library

            // Standard termination files for executables  
            if (settings.OutputType == ModuleType.Executable || settings.OutputType == ModuleType.ExecutableWin32)
            {
                var crtPath = GetCrtPath();
                if (!string.IsNullOrEmpty(crtPath))
                {
                    arguments.Add($"\"{Path.Combine(crtPath, "crtn.o")}\"");
                }
            }

            // Additional linker flags
            arguments.AddRange(settings.LinkerFlags);

            var startInfo = new ProcessStartInfo
            {
                FileName = _ldExecutablePath,
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

        private string? GetCrtPath()
        {
            // Common locations for CRT files
            var possiblePaths = new[]
            {
                "/usr/lib/x86_64-linux-gnu",
                "/usr/lib/i386-linux-gnu", 
                "/usr/lib",
                "/lib/x86_64-linux-gnu",
                "/lib/i386-linux-gnu",
                "/lib"
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "crt1.o")))
                {
                    return path;
                }
            }
            return null;
        }
    }
}