using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ebuild.api;
using ebuild.Platforms;
using ebuild.Compilers;
using ebuild.Linkers;
using ebuild.api.Linker;

namespace ebuild.Tests.Integration
{
    [TestFixture]
    public class ZlibEbuildTests
    {
        private string _zlibModulePath;
        private string _testOutputDir;
        private string _ebuildExePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _zlibModulePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "zlib", "zlib.ebuild.cs");
            _testOutputDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "zlib");
            _ebuildExePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "bin", "Debug", "net8.0", "ebuild.dll");

            Directory.CreateDirectory(_testOutputDir);
        }
        [Test]
        public async Task ZlibEbuild_Should_Build_StaticLibrary_Successfully()
        {
            // Arrange
            var workingDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "zlib");
            var zlibModulePath = Path.Combine(workingDirectory, "zlib.ebuild.cs");

            // Ensure the module file exists
            Assert.That(File.Exists(zlibModulePath), Is.True, $"zlib.ebuild.cs should exist at {zlibModulePath}");

            // Act
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "ebuild.csproj")}\" -- build static:zlib.ebuild.cs --clean",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null, "Process should start successfully");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Assert
            Console.WriteLine($"Standard Output: {output}");
            Console.WriteLine($"Standard Error: {error}");

            // The process should exit cleanly (exit code 0 indicates success)
            Assert.That(process.ExitCode, Is.EqualTo(0), $"Build should succeed. Exit code: {process.ExitCode}, Error: {error}");

            // Check that the correct artifacts were created
            var buildDir = Path.Combine(workingDirectory, ".ebuild", "zlib", "build");
            Assert.That(Directory.Exists(buildDir), Is.True, "Build directory should exist");

            // Check for object files (compiled source files)
            var objectFiles = Directory.GetFiles(buildDir, "*.o", SearchOption.AllDirectories);
            if (objectFiles.Length == 0)
            {
                // Fallback to Windows .obj files if on Windows platform
                objectFiles = Directory.GetFiles(buildDir, "*.obj", SearchOption.AllDirectories);
            }
            Assert.That(objectFiles, Is.Not.Empty, "Should have compiled object files");

            // Verify that the appropriate linker is available and properly registered
            var platform = PlatformRegistry.GetHostPlatform();
            // Check for static library files in the correct directory (Binaries)
            var binariesDir = Path.Combine(workingDirectory, "Binaries");
            if (Directory.Exists(binariesDir))
            {
                var staticLibFiles = Directory.GetFiles(binariesDir, "*.lib", SearchOption.AllDirectories);
                if (staticLibFiles.Length > 0)
                {
                    Console.WriteLine("Static library files found:");
                    foreach (var libFile in staticLibFiles)
                    {
                        Console.WriteLine($"  {libFile}");
                    }
                }
                else
                {
                    // Try checking for .a files (Unix static libraries)
                    var unixStaticLibFiles = Directory.GetFiles(binariesDir, "*.a", SearchOption.AllDirectories);
                    if (unixStaticLibFiles.Length > 0)
                    {
                        Console.WriteLine("Unix static library files found:");
                        foreach (var libFile in unixStaticLibFiles)
                        {
                            Console.WriteLine($"  {libFile}");
                        }
                    }
                    else
                    {
                        Assert.Fail("Static library files should be created since linker is available and setup succeeded");
                    }
                }
            }
            else
            {
                Assert.Fail("Binaries directory should exist after successful linking");
            }

            // Verify some expected object files exist (from known zlib source files)
            var expectedObjectFiles = new[] { "adler32.o", "compress.o", "crc32.o", "deflate.o", "inflate.o" };
            foreach (var expectedFile in expectedObjectFiles)
            {
                var found = objectFiles.Any(f => Path.GetFileName(f) == expectedFile);
                // If .o files not found, try .obj files (for Windows)
                if (!found)
                {
                    var expectedObjFile = Path.ChangeExtension(expectedFile, ".obj");
                    found = objectFiles.Any(f => Path.GetFileName(f) == expectedObjFile);
                }
                Assert.That(found, Is.True, $"Expected object file {expectedFile} (or .obj variant) should exist");
            }
        }

        [Test]
        public async Task ZlibEbuild_Should_Build_SharedLibrary_Successfully()
        {
            // Arrange
            var workingDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "zlib");
            var zlibModulePath = Path.Combine(workingDirectory, "zlib.ebuild.cs");

            // Ensure the module file exists
            Assert.That(File.Exists(zlibModulePath), Is.True, $"zlib.ebuild.cs should exist at {zlibModulePath}");

            // Act - Build as shared library
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "ebuild.csproj")}\" -- build shared:zlib.ebuild.cs --clean",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            using var process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null, "Process should start successfully");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            process.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);
            await process.WaitForExitAsync();
            // The process should exit cleanly
            Assert.That(process.ExitCode, Is.EqualTo(0), $"Build should succeed. Exit code: {process.ExitCode}");

            // Check for shared library files
            var binariesDir = Path.Combine(workingDirectory, "Binaries");
            Assert.That(Directory.Exists(binariesDir), Is.True, "Binaries directory should exist after successful linking");

            // Look for shared library files (.dll on Windows, .so on Unix)
            var sharedLibFiles = Directory.GetFiles(binariesDir, "*.dll", SearchOption.AllDirectories);
            if (sharedLibFiles.Length == 0)
            {
                // Try Unix shared libraries
                sharedLibFiles = Directory.GetFiles(binariesDir, "*.so", SearchOption.AllDirectories);
            }

            Assert.That(sharedLibFiles, Is.Not.Empty, "Should have created shared library files");

            Console.WriteLine("Shared library files found:");
            foreach (var libFile in sharedLibFiles)
            {
                Console.WriteLine($"  {libFile}");
                // Verify that the file is not empty
                var fileInfo = new FileInfo(libFile);
                Assert.That(fileInfo.Length, Is.GreaterThan(0), $"Shared library file {libFile} should not be empty");
            }
        }

        [Test]
        public async Task ZlibEbuild_Should_Build_With_Options_Successfully()
        {
            // Arrange
            var workingDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "zlib");
            var zlibModulePath = Path.Combine(workingDirectory, "zlib.ebuild.cs");

            // Ensure the module file exists
            Assert.That(File.Exists(zlibModulePath), Is.True, $"zlib.ebuild.cs should exist at {zlibModulePath}");

            // Act - Build with options that should change the binary output
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "ebuild.csproj")}\" -- build \"static:zlib.ebuild.cs?EnableDebug=true;EnableAdvancedFeatures=true;OptimizeForSize=true\" --clean",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            Assert.That(process, Is.Not.Null, "Process should start successfully");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Assert
            Console.WriteLine($"Standard Output: {output}");
            Console.WriteLine($"Standard Error: {error}");

            // The process should exit cleanly
            Assert.That(process.ExitCode, Is.EqualTo(0), $"Build should succeed. Exit code: {process.ExitCode}, Error: {error}");

            // Check for library files
            var binariesDir = Path.Combine(workingDirectory, "Binaries");
            Assert.That(Directory.Exists(binariesDir), Is.True, "Binaries directory should exist after successful linking");

            // Look for static library files
            var staticLibFiles = Directory.GetFiles(binariesDir, "*.lib", SearchOption.AllDirectories);
            if (staticLibFiles.Length == 0)
            {
                // Try Unix static libraries
                staticLibFiles = Directory.GetFiles(binariesDir, "*.a", SearchOption.AllDirectories);
            }

            Assert.That(staticLibFiles, Is.Not.Empty, "Should have created static library files with options");

            Console.WriteLine("Static library files found with options:");
            foreach (var libFile in staticLibFiles)
            {
                Console.WriteLine($"  {libFile}");
                // Verify that the file is not empty
                var fileInfo = new FileInfo(libFile);
                Assert.That(fileInfo.Length, Is.GreaterThan(0), $"Static library file {libFile} should not be empty");
            }

            // Verify that different options create different transformed build directories (different paths)
            // The build should be in a different directory than the default build due to transformed build
            var transformerDirs = Directory.GetDirectories(binariesDir);
            Assert.That(transformerDirs, Is.Not.Empty, "Should have transformed directories");

            Console.WriteLine("Transformed directories found:");
            foreach (var dir in transformerDirs)
            {
                Console.WriteLine($"  {dir}");
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any temporary files created during testing
            try
            {
                if (Directory.Exists(_testOutputDir))
                {
                    Directory.Delete(_testOutputDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}