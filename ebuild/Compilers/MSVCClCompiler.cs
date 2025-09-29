using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Compilers
{
    public partial class MsvcClCompiler : CompilerBase
    {

        public static void InitPaths(Architecture targetArchitecture)
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

            if (string.IsNullOrEmpty(toolRoot))
            {
                throw new Exception("MSVC tool root couldn't be found, MSVC Compiler and linker setup has failed");

            }

            var version = MSVCUtils.FindMsvcVersion(toolRoot).Result;
            if (string.IsNullOrEmpty(version))
            {
                throw new Exception("Couldn't find a valid msvc installation.");
            }

            var (MsvcToolRoot, MsvcToolsBinRoot) = MSVCUtils.SetupMsvcPaths(toolRoot, version);
            MsvcToolsBinPath = Path.Join(MsvcToolsBinRoot, targetArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => "x86"
            });
            CLExecutablePath = Path.Join(MsvcToolsBinPath, "cl.exe");
            MsvcIncludePath = Path.Join(MsvcToolRoot, "include");
            PathsInitialized = true;
        }

        public MsvcClCompiler(Architecture targetArchitecture)
        {
            InitPaths(targetArchitecture);
        }
        private static bool PathsInitialized = false;
        public static string CLExecutablePath = "cl.exe";
        public static string MsvcToolsBinPath = string.Empty;
        public static string MsvcIncludePath = string.Empty;

        public override Task<bool> Generate(CompilerSettings settings, CancellationToken cancellationToken, string type, object? data = null)
        {
            if (type == "compile_commands.json" && data is List<JsonObject> objectList)
            {
                var args = GetCommand(settings, true);
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

        private async Task WaitForWriteAccess(string filePath, CancellationToken cancellationToken)
        {
            var retries = 100;
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Invalid file path", nameof(filePath));
            }

            while (true)
            {
                try
                {
                    // Try to create a temporary file in the target directory
                    var tempFilePath = Path.Combine(directory, Path.GetRandomFileName());
                    using (var fs = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        // Successfully created the file, we have write access
                    }
                    File.Delete(tempFilePath); // Clean up the temporary file
                    break; // Exit the loop if we have write access
                }
                catch (IOException exception)
                {
                    // If an IOException occurs, it means we don't have write access yet
                    Console.WriteLine($"Waiting for write access to {filePath}. Exception: {exception.Message}");
                    retries--;
                    if (retries <= 0)
                    {
                        throw new IOException($"Exceeded maximum retries while waiting for write access to {filePath}.");
                    }
                    await Task.Delay(100, cancellationToken); // Wait a bit before retrying
                }
            }
        }


        public ArgumentBuilder GetCommand(CompilerSettings settings, bool forCompileCommands)
        {
            // Prepare the command line arguments for cl.exe based on CompilerSettings
            var args = new ArgumentBuilder();
            if (forCompileCommands)
            {
                args.Add(CLExecutablePath);
            }
            args.Add("/nologo"); // Suppress startup banner
            args.Add("/c"); // Compile only, do not link
            args.Add("/utf-8"); // Specify source file character set
            args.Add("/volatile:iso"); // Use ISO C++ volatile semantics
            args.Add("/permissive-"); // Standards conformance

            args.Add("/Zc:__cplusplus"); // Ensure __cplusplus is correctly defined
            args.Add("/Zc:enumTypes"); // Enable strongly typed enums
            args.Add("/Zc:externC");
            args.Add("/Zc:forScope"); // Enforce for loop scope rules
            args.Add("/Zc:gotoScope");
            args.Add("/Zc:hiddenFriend");
            args.Add("/Zc:implicitNoexcept"); // Enforce implicit noexcept rules
            args.Add("/Zc:inline"); // Enforce inline rules
            args.Add("/Zc:rvalueCast"); // Enforce rvalue cast rules
            args.Add("/Zc:externConstexpr"); // Enforce extern constexpr rules
            args.Add("/Zc:strictStrings");
            args.Add("/Zc:templateScope");
            args.Add("/Zc:preprocessor");
            args.Add("/Zc:referenceBinding");
            args.Add("/Zc:sizedDealloc"); // Enable sized deallocation
            args.Add("/Zc:threadSafeInit"); // Thread-safe static initialization
            if (settings.EnableExceptions)
            {
                args.Add("/Zc:throwingNew");
            }
            args.Add("/Zc:wchar_t"); // Treat wchar_t as a native type



            if (settings.CPUExtension != CPUExtensions.Default)
            {
                args.Add($"/arch:{settings.CPUExtension switch
                {

                    CPUExtensions.IA32 => "IA32",
                    CPUExtensions.SSE => "SSE",
                    CPUExtensions.SSE2 => "SSE2",
                    CPUExtensions.SSE4_2 => "SSE4.2",
                    CPUExtensions.AVX => "AVX",
                    CPUExtensions.AVX2 => "AVX2",
                    CPUExtensions.AVX512 => "AVX512",
                    CPUExtensions.AVX10_1 => "AVX10.1",
                    CPUExtensions.ARMv7VE => "ARMv7VE",
                    CPUExtensions.VFPv4 => "VFPv4",
                    CPUExtensions.armv8_0 => "armv8.0",
                    CPUExtensions.armv8_1 => "armv8.1",
                    CPUExtensions.armv8_2 => "armv8.2",
                    CPUExtensions.armv8_3 => "armv8.3",
                    CPUExtensions.armv8_4 => "armv8.4",
                    CPUExtensions.armv8_5 => "armv8.5",
                    CPUExtensions.armv8_6 => "armv8.6",
                    CPUExtensions.armv8_7 => "armv8.7",
                    CPUExtensions.armv8_8 => "armv8.8",
                    CPUExtensions.armv8_9 => "armv8.9",
                    CPUExtensions.armv9_0 => "armv9.0",
                    CPUExtensions.armv9_1 => "armv9.1",
                    CPUExtensions.armv9_2 => "armv9.2",
                    CPUExtensions.armv9_3 => "armv9.3",
                    CPUExtensions.armv9_4 => "armv9.4",
                    CPUExtensions.Default => throw new NotSupportedException($"The specified CPU extension is not supported by MSVC: {settings.CPUExtension}"),
                    _ => throw new NotSupportedException($"The specified CPU extension is not supported by MSVC: {settings.CPUExtension}")
                }}");
            }
            args.Add($"/Fo\"{settings.OutputFile}\""); // Specify output file
            if (settings.CStandard != null)
            {
                args.Add("/TC"); // Treat input as C code
                if (settings.CStandard is CStandards.C89 or CStandards.C99)
                {
                    if (settings.CStandard == CStandards.C89)
                        args.Add("/Za"); // Disable language extensions to enforce strict ANSI compliance                        
                }
                else
                {
                    args.Add("/Zc:__STDC__"); // Ensure c standard macros are correctly defined
                    args.Add($"/std:{settings.CStandard switch
                    {
                        CStandards.C89 => throw new NotSupportedException("C89 is not supported by MSVC"),
                        CStandards.C99 => throw new NotSupportedException("C99 is not supported by MSVC"),
                        CStandards.C11 => "c11",
                        CStandards.C17 => "c17",
                        _ => "c17"
                    }}");
                }
            }
            else
            {
                args.Add($"/std:{settings.CppStandard switch
                {
                    CppStandards.Cpp98 => throw new NotSupportedException("C++98 is not supported by MSVC"),
                    CppStandards.Cpp03 => throw new NotSupportedException("C++03 is not supported by MSVC"),
                    CppStandards.Cpp11 => throw new Exception("C++11 is not supported by MSVC"),
                    CppStandards.Cpp14 => "c++14",
                    CppStandards.Cpp17 => "c++17",
                    CppStandards.Cpp20 => "c++20",
                    CppStandards.Cpp23 => "c++23preview",
                    _ => "c++20"
                }}");
                args.Add("/TP"); // Treat input as C++ code
                if (settings.CppStandard >= CppStandards.Cpp20)
                {
                    args.Add("/await:strict");
                }
                if (settings.EnableExceptions)
                {
                    args.Add("/EHsc"); // Enable C++ exceptions
                }
                else
                {
                    args.Add("/D_HAS_EXCEPTIONS=0"); // Define _HAS_EXCEPTIONS=0 to disable STL exceptions
                    args.Add("/EHs-c-"); // Disable C++ exceptions
                }
            }
            foreach (var def in settings.Definitions)
            {
                if (def.HasValue())
                    args.Add($"/D{def.GetName()}={def.GetValue()}");
                else
                    args.Add($"/D{def.GetName()}");
            }
            args.Add($"/I\"{MsvcIncludePath}\""); // Add MSVC include path
            foreach (var includePath in settings.IncludePaths)
            {
                args.Add($"/I\"{includePath}\"");
            }
            foreach (var forceInclude in settings.ForceIncludes)
            {
                args.Add($"/FI\"{forceInclude}\"");
            }
            if (settings.IsDebugBuild)
            {
                args.Add("/MDd"); // Use the debug version of the static runtime
                args.Add("/RTC1"); // Enable runtime error checks
                if (!settings.OtherFlags.Contains("/sdl-"))
                {
                    args.Add("/sdl"); // Enable additional security checks    
                }

            }
            else
            {
                args.Add("/MD");
                if(settings.ModuleType != ModuleType.StaticLibrary)
                    args.Add("/GL");
                args.Add("/Gy");
            }
            if (settings.EnableDebugFileCreation)
            {
                args.Add("/Zi"); // Generate complete debugging information
                args.Add($"/Fd\"{Path.ChangeExtension(settings.OutputFile, ".pdb")}\""); // Specify PDB file for debug info   
            }
            if (settings.EnableFastFloatingPointOperations && settings.CStandard != CStandards.C89)
            {
                args.Add("/fp:fast"); // Enable fast floating-point model
            }
            if (settings.EnableRTTI)
            {
                args.Add("/GR"); // Enable RTTI
            }
            else
            {
                args.Add("/GR-"); // Disable RTTI
            }
            args.Add(settings.IsDebugBuild ? "/Od" : settings.Optimization switch
            {
                OptimizationLevel.None => "/Od", // Disable optimization
                OptimizationLevel.Size => "/O1", // Optimize for size
                OptimizationLevel.Speed => "/O2", // Optimize for speed
                OptimizationLevel.Max => "/Ox", // Full optimization
                _ => "/O2"
            });
            args.AddRange(settings.OtherFlags); // Add any other custom flags
            args.Add($"\"{settings.SourceFile}\""); // Input source file


            return args;
        }

        public override async Task<bool> Compile(CompilerSettings settings, CancellationToken cancellationToken)
        {
            // Create output directory if it doesn't exist
            var outputDir = Path.GetDirectoryName(settings.OutputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            // Wait for write access to the directory
            await WaitForWriteAccess(settings.OutputFile, cancellationToken);
            var args = GetCommand(settings, false);
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, args.ToString(), cancellationToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = CLExecutablePath,
                Arguments = "@\"" + tempFile + "\"",
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
            try { File.Delete(tempFile); } catch { /* ignore errors from deleting temp file */ }
            return process.ExitCode == 0;
        }
    }
}
