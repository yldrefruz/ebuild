using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Compilers
{
    public class GccCompiler : CompilerBase
    {
        private readonly Architecture _targetArchitecture;
        private readonly string _gccExecutablePath;
        private readonly string _gxxExecutablePath;

        public GccCompiler(Architecture targetArchitecture)
        {
            _targetArchitecture = targetArchitecture;
            _gccExecutablePath = FindExecutable("gcc") ?? throw new Exception("GCC compiler not found in PATH");
            _gxxExecutablePath = FindExecutable("g++") ?? throw new Exception("G++ compiler not found in PATH");
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
                
                // Try with .exe extension on Windows
                var exePath = fullPath + ".exe";
                if (File.Exists(exePath))
                    return exePath;
            }
            return null;
        }


        public ArgumentBuilder GenerateCompileCommand(CompilerSettings settings, bool forCompileCommands)
        {
            // Prepare the command line arguments for gcc/g++
            var args = new ArgumentBuilder();

            if (forCompileCommands)
            {
                args.Add(settings.CStandard != null ? _gccExecutablePath : _gxxExecutablePath);
            }

            // Basic compilation flags
            args.Add("-c"); // Compile only, do not link
            args.Add("-o");
            args.Add(settings.OutputFile); // Specify output file

            // Language standard
            if (settings.CStandard != null)
            {
                args.Add($"-std={settings.CStandard switch
                {
                    CStandards.C89 => "c89",
                    CStandards.C99 => "c99",
                    CStandards.C11 => "c11",
                    CStandards.C17 => "c17",
                    _ => "c17"
                }}");
            }
            else
            {
                args.Add($"-std={settings.CppStandard switch
                {
                    CppStandards.Cpp98 => "c++98",
                    CppStandards.Cpp03 => "c++03",
                    CppStandards.Cpp11 => "c++11",
                    CppStandards.Cpp14 => "c++14",
                    CppStandards.Cpp17 => "c++17",
                    CppStandards.Cpp20 => "c++20",
                    CppStandards.Cpp23 => "c++23",
                    _ => "c++20"
                }}");

                if (settings.EnableExceptions)
                {
                    args.Add("-fexceptions"); // Enable C++ exceptions
                }
                else
                {
                    args.Add("-fno-exceptions"); // Disable C++ exceptions
                }

                if (settings.EnableRTTI)
                {
                    args.Add("-frtti"); // Enable RTTI
                }
                else
                {
                    args.Add("-fno-rtti"); // Disable RTTI
                }
            }

            // Architecture-specific flags
            if (_targetArchitecture == Architecture.X86)
            {
                args.Add("-m32");
            }
            else if (_targetArchitecture == Architecture.X64)
            {
                args.Add("-m64");
            }
            else if (_targetArchitecture == Architecture.Arm)
            {
                args.Add("-march=armv7-a");
            }
            else if (_targetArchitecture == Architecture.Arm64)
            {
                args.Add("-march=armv8-a");
            }
            else if (_targetArchitecture == Architecture.Armv6)
            {
                args.Add("-march=armv6");
            }
            else if (_targetArchitecture == Architecture.Wasm)
            {
                // WebAssembly target would need special handling
                args.Add("-target");
                args.Add("wasm32");
            }
            else if (_targetArchitecture == Architecture.S390x)
            {
                args.Add("-march=z196");
            }
            else if (_targetArchitecture == Architecture.LoongArch64)
            {
                args.Add("-march=loongarch64");
            }
            else if (_targetArchitecture == Architecture.Ppc64le)
            {
                args.Add("-mcpu=power8");
            }

            // CPU extensions
            if (settings.CPUExtension != CPUExtensions.Default)
            {
                args.Add($"-march={settings.CPUExtension switch
                {
                    CPUExtensions.SSE => "pentium3",
                    CPUExtensions.SSE2 => "pentium4",
                    CPUExtensions.SSE4_2 => "nehalem",
                    CPUExtensions.AVX => "sandybridge",
                    CPUExtensions.AVX2 => "haswell",
                    CPUExtensions.AVX512 => "skylake-avx512",
                    CPUExtensions.armv8_0 => "armv8-a",
                    CPUExtensions.armv8_1 => "armv8.1-a",
                    CPUExtensions.armv8_2 => "armv8.2-a",
                    CPUExtensions.armv8_3 => "armv8.3-a",
                    CPUExtensions.armv8_4 => "armv8.4-a",
                    CPUExtensions.armv8_5 => "armv8.5-a",
                    CPUExtensions.armv8_6 => "armv8.6-a",
                    CPUExtensions.armv8_7 => "armv8.7-a",
                    CPUExtensions.armv8_8 => "armv8.8-a",
                    CPUExtensions.armv8_9 => "armv8.9-a",
                    CPUExtensions.armv9_0 => "armv9-a",
                    CPUExtensions.armv9_1 => "armv9.1-a",
                    CPUExtensions.armv9_2 => "armv9.2-a",
                    CPUExtensions.armv9_3 => "armv9.3-a",
                    CPUExtensions.armv9_4 => "armv9.4-a",
                    _ => "native"
                }}");
            }

            // Optimization level
            args.Add(settings.Optimization switch
            {
                OptimizationLevel.None => "-O0",
                OptimizationLevel.Size => "-Os",
                OptimizationLevel.Speed => "-O2",
                OptimizationLevel.Max => "-O3",
                _ => "-O2"
            });

            // Debug information - setup debug file names like MSVC does
            if (settings.EnableDebugFileCreation)
            {
                args.Add("-g"); // Generate debug information
            }

            // Preprocessor definitions
            foreach (var def in settings.Definitions)
            {
                if (def.HasValue())
                    args.Add($"-D{def.GetName()}={def.GetValue()}");
                else
                    args.Add($"-D{def.GetName()}");
            }

            // Include paths
            foreach (var includePath in settings.IncludePaths)
            {
                args.Add("-I");
                args.Add(includePath);
            }

            // Force includes
            foreach (var forceInclude in settings.ForceIncludes)
            {
                args.Add("-include");
                args.Add(forceInclude);
            }

            // Fast floating point operations
            if (settings.EnableFastFloatingPointOperations)
            {
                args.Add("-ffast-math");
            }

            // Position independent code for shared libraries (required on many platforms)
            // if (settings.ModuleType == ModuleType.SharedLibrary)
            {
                args.Add("-fPIC");
            }

            // Additional custom flags
            args.AddRange(settings.OtherFlags);

            // Source file (must be last)
            args.Add(settings.SourceFile);

            return args;
        }

        public override Task<bool> Generate(CompilerSettings settings, CancellationToken cancellationToken, string type, object? data = null)
        {
                if (type == "compile_commands.json" && data is List<JsonObject> objectList)
            {
                var args = GenerateCompileCommand(settings, true);
                var commandEntry = new JsonObject
                {
                    ["directory"] = Path.GetDirectoryName(settings.SourceFile) ?? Environment.CurrentDirectory,
                    ["command"] = args.ToString(),
                    ["output"] = settings.OutputFile,
                    ["file"] = settings.SourceFile
                };
                objectList.Add(commandEntry);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public override async Task<bool> Compile(CompilerSettings settings, CancellationToken cancellationToken)
        {
            // Use the output file as provided by the platform
            var outputFile = settings.OutputFile;

            // Create output directory if it doesn't exist (both the final dir and any intermediate directories)
            var outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }



            // Determine whether to use gcc or g++ based on settings
            var compilerPath = settings.CStandard != null ? _gccExecutablePath : _gxxExecutablePath;
            var args = GenerateCompileCommand(settings, false);


            var startInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
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
    }
}