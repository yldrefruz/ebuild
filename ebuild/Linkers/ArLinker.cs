using System.Diagnostics;
using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Linker;
using ebuild.Platforms;
using Microsoft.VisualBasic;

namespace ebuild.Linkers
{
    public class ArLinkerFactory : ILinkerFactory
    {
        public string Name => "ar";

        public Type LinkerType => typeof(ArLinker);

        public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            // AR is available if ar exists on PATH and only for static libraries
            return FindExecutable("ar") != null && module.Type == ModuleType.StaticLibrary;
        }

        public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
        {
            return new ArLinker(instancingParams.Architecture, instancingParams.Platform, module.CStandard != null);
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

    public class ArLinker : LinkerBase
    {
        private readonly Architecture _targetArchitecture;
        private readonly string _arExecutablePath;
        private readonly PlatformBase _platform;
        private readonly bool _isCModule = false;

        public ArLinker(Architecture targetArchitecture, PlatformBase? platform = null, bool isCModule = false)
        {
            _targetArchitecture = targetArchitecture;
            _arExecutablePath = FindExecutable("ar") ?? throw new Exception("AR archiver not found in PATH");
            _platform = platform ?? PlatformRegistry.GetHostPlatform();
            _isCModule = isCModule;
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

        private class TemporaryDirectoriesList : List<string>, IDisposable
        {
            public void Dispose()
            {
                foreach (var item in this)
                {
                    Directory.Delete(item, true);
                }
            }
        }

        public override async Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken = default)
        {
            using TemporaryDirectoriesList tempDirs = new();
            if (settings.OutputType != ModuleType.StaticLibrary)
            {
                throw new NotSupportedException("AR archiver only supports creating static libraries. Use LD for executables and shared libraries.");
            }
            var objectInputs = settings.InputFiles.Where(f => Path.GetExtension(f) == _platform.ExtensionForCompiledSourceFile).ToList();
            var combineLibraries = settings.InputFiles.Where(f => Path.GetExtension(f) == _platform.ExtensionForStaticLibrary);
            // No support for combining shared libraries into static libraries.
            // They should be public dependencies so that the linker can link them while compiling with a shared library or executable.
            var isCombine = combineLibraries?.Any() ?? false;
            if (isCombine)
            {
                // run ar to extract object files from the static libraries.
                foreach (var lib in combineLibraries!)
                {
                    var extractArgs = new ArgumentBuilder();
                    // Create a temporary directory to extract the object files
                    var outputDirForLib = Path.Join(settings.IntermediateDir, "combine", "extracted", Path.GetFileNameWithoutExtension(lib));
                    tempDirs.Add(outputDirForLib);

                    extractArgs.Add("x"); // x = extract
                    extractArgs.Add($"\"{lib}\"");
                    var extractTempFile = Path.GetTempFileName();
                    // extract in the IntermediateDir/combine/extracted/LibName/
                    if (Directory.Exists(outputDirForLib))
                    {
                        Directory.Delete(outputDirForLib, true);
                    }
                    Directory.CreateDirectory(outputDirForLib);

                    await File.WriteAllTextAsync(extractTempFile, extractArgs.ToString(), cancellationToken);
                    var extractStartInfo = new ProcessStartInfo
                    {
                        FileName = _arExecutablePath,
                        Arguments = $"@\"{extractTempFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = outputDirForLib
                    };
                    var extractProcess = new Process { StartInfo = extractStartInfo };
                    extractProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                    extractProcess.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);
                    extractProcess.Start();
                    extractProcess.BeginOutputReadLine();
                    extractProcess.BeginErrorReadLine();
                    await extractProcess.WaitForExitAsync(cancellationToken);
                    File.Delete(extractTempFile);
                    if (extractProcess.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Failed to extract object files from {lib}");
                        return false;
                    }
                    objectInputs.AddRange(Directory.GetFiles(outputDirForLib, "*" + _platform.ExtensionForCompiledSourceFile, SearchOption.AllDirectories));
                }
            }

            var arguments = new ArgumentBuilder();
            var outputDir = Path.GetDirectoryName(settings.OutputFile);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            // AR operation flags
            arguments.Add("rcs"); // r = insert files, c = create archive if it doesn't exist, s = write symbol table

            // Output archive file
            arguments.Add(settings.OutputFile);
            // Input object files only
            arguments.AddRange(objectInputs);

            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, arguments.ToString(), cancellationToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = _arExecutablePath,
                Arguments = $"@\"{tempFile}\"",
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
            File.Delete(tempFile);
            return process.ExitCode == 0;
        }
    }
}