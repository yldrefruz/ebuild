using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ebuild.api;
using ebuild.Modules.BuildGraph;

namespace ebuild.Tests.Integration
{
    [TestFixture]
    public class CircularDependencyTests
    {
        private string _circularDependencyExamplePath;
        private string _testModuleAPath;
        private string _testModuleBPath;
        private string _testOutputDir;
        private string _ebuildExePath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _circularDependencyExamplePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "circular-dependency");
            _testModuleAPath = Path.Combine(_circularDependencyExamplePath, "test_circular_a.ebuild.cs");
            _testModuleBPath = Path.Combine(_circularDependencyExamplePath, "test_circular_b.ebuild.cs");
            _testOutputDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "circular_dependency");
            _ebuildExePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "bin", "Debug", "net8.0", "ebuild.dll");

            Directory.CreateDirectory(_testOutputDir);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (Directory.Exists(_testOutputDir))
            {
                Directory.Delete(_testOutputDir, true);
            }
        }

        [Test]
        public void CircularDependencyDetection_ShouldDetectCircularDependency()
        {
            // Arrange
            Assert.That(File.Exists(_testModuleAPath), $"Test module A not found at: {_testModuleAPath}");
            Assert.That(File.Exists(_testModuleBPath), $"Test module B not found at: {_testModuleBPath}");
            Assert.That(File.Exists(_ebuildExePath), $"EBuild executable not found at: {_ebuildExePath}");

            // Act
            var result = RunEBuildCommand($"check circular-dependencies \"{_testModuleAPath}\"");

            // Assert
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), "Expected non-zero exit code for circular dependency detection");
            Assert.That(result.Output, Does.Contain("Circular dependency detected"), "Expected circular dependency detection message");
            Assert.That(result.Output, Does.Contain("TestModuleA"), "Expected TestModuleA in the circular dependency path");
            Assert.That(result.Output, Does.Contain("TestModuleB"), "Expected TestModuleB in the circular dependency path");
        }

        [Test]
        public void CircularDependencyDetection_ShouldNotHang()
        {
            // Arrange
            Assert.That(File.Exists(_testModuleAPath), $"Test module A not found at: {_testModuleAPath}");

            // Act & Assert
            var stopwatch = Stopwatch.StartNew();
            var result = RunEBuildCommand($"check circular-dependencies \"{_testModuleAPath}\"", timeoutSeconds: 30);
            stopwatch.Stop();

            // Should complete within reasonable time (30 seconds timeout, but should be much faster)
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(30), "Circular dependency detection should not hang");
            Assert.That(result.TimedOut, Is.False, "Command should not timeout");
        }

        [Test]
        public Task BuildGraph_ShouldHandleCircularDependencyGracefully()
        {
            // Arrange
            var moduleFile = ModuleFile.Get(new ModuleReference(_testModuleAPath), new ModuleReference(_testModuleAPath));
            var instancingParams = new ModuleInstancingParams
            {
                SelfModuleReference = new ModuleReference(_testModuleAPath),
                Configuration = "Debug",
                Architecture = System.Runtime.InteropServices.Architecture.X64,
                Toolchain = new ebuild.Toolchains.GccToolchain(),
                Platform = new ebuild.Platforms.UnixPlatform(),
                AdditionalLinkerOptions = new List<string>()
            };

            // Act
            Graph buildGraph = null;
            Assert.DoesNotThrowAsync(async () =>
            {
                buildGraph = await moduleFile.BuildOrGetBuildGraph(instancingParams);
            });

            // Assert
            Assert.That(buildGraph, Is.Not.Null, "Build graph should be created even with circular dependencies");
            Assert.That(buildGraph.HasCircularDependency(), Is.True, "Build graph should detect circular dependency");

            var circularPath = buildGraph.GetCircularDependencyPath();
            Assert.That(circularPath, Is.Not.Empty, "Circular dependency path should not be empty");
            Assert.That(circularPath.Count, Is.GreaterThan(1), "Circular dependency path should contain multiple nodes");
            return Task.CompletedTask;
        }

        [Test]
        public async Task BuildGraph_CachingWorksCorrectly()
        {
            // Arrange
            var moduleFile = ModuleFile.Get(new ModuleReference(_testModuleAPath), new ModuleReference(_testModuleAPath));
            var instancingParams = new ModuleInstancingParams
            {
                SelfModuleReference = new ModuleReference(_testModuleAPath),
                Configuration = "Debug",
                Architecture = System.Runtime.InteropServices.Architecture.X64,
                Toolchain = new ebuild.Toolchains.GccToolchain(),
                Platform = new ebuild.Platforms.UnixPlatform(),
                AdditionalLinkerOptions = new List<string>()
            };

            var buildGraph = await moduleFile.BuildOrGetBuildGraph(instancingParams);
            Assert.That(buildGraph, Is.Not.Null);

            // Act - Call multiple times to test caching
            var stopwatch = Stopwatch.StartNew();
            var hasCircular1 = buildGraph.HasCircularDependency();
            var time1 = stopwatch.ElapsedMilliseconds;
            
            var hasCircular2 = buildGraph.HasCircularDependency();
            var time2 = stopwatch.ElapsedMilliseconds - time1;
            
            var path1 = buildGraph.GetCircularDependencyPath();
            var time3 = stopwatch.ElapsedMilliseconds - time1 - time2;
            
            var path2 = buildGraph.GetCircularDependencyPath();
            var time4 = stopwatch.ElapsedMilliseconds - time1 - time2 - time3;

            // Assert
            Assert.That(hasCircular1, Is.EqualTo(hasCircular2), "Cached results should be consistent");
            Assert.That(path1.Count, Is.EqualTo(path2.Count), "Cached path results should be consistent");
            
            // Second calls should be faster due to caching (allowing for some variance)
            Assert.That(time2, Is.LessThanOrEqualTo(time1), "Second HasCircularDependency call should be faster (cached)");
            Assert.That(time4, Is.LessThanOrEqualTo(time3), "Second GetCircularDependencyPath call should be faster (cached)");
        }

        [Test]
        public void PrintDependencies_ShouldShowCircularDependencyInGraph()
        {
            // Arrange
            Assert.That(File.Exists(_testModuleAPath), $"Test module A not found at: {_testModuleAPath}");
            Assert.That(File.Exists(_ebuildExePath), $"EBuild executable not found at: {_ebuildExePath}");

            // Act
            var result = RunEBuildCommand($"check print-dependencies \"{_testModuleAPath}\"");

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0), "Expected zero exit code for print-dependencies command");
            Assert.That(result.Output, Does.Contain("Dependencies for"), "Expected dependencies header");
            Assert.That(result.Output, Does.Contain("TestModuleA"), "Expected TestModuleA in the output");
            Assert.That(result.Output, Does.Contain("TestModuleB"), "Expected TestModuleB in the output");
            Assert.That(result.Output, Does.Contain("(circular dependency)"), "Expected circular dependency notation in the output");
            
            // Verify the tree structure shows the circular dependency properly
            var lines = result.Output.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            var dependencyLines = lines.SkipWhile(line => !line.Contains("Dependencies for")).Skip(1).ToArray();
            
            Assert.That(dependencyLines.Length, Is.GreaterThan(0), "Should have dependency tree output");
            Assert.That(dependencyLines.Any(line => line.Contains("TestModuleA") && !line.Contains("circular")), Is.True, "Should show TestModuleA as root");
            Assert.That(dependencyLines.Any(line => line.Contains("TestModuleB")), Is.True, "Should show TestModuleB as dependency");
            Assert.That(dependencyLines.Any(line => line.Contains("TestModuleA (circular dependency)")), Is.True, "Should show circular dependency notation");
        }

        private (int ExitCode, string Output, bool TimedOut) RunEBuildCommand(string arguments, int timeoutSeconds = 60)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{_ebuildExePath}\" {arguments}",
                WorkingDirectory = _circularDependencyExamplePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) => {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (sender, e) => {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = process.WaitForExit(timeoutSeconds * 1000);
            if (!completed)
            {
                process.Kill();
                return (-1, "Process timed out", true);
            }

            var output = outputBuilder.ToString() + errorBuilder.ToString();
            return (process.ExitCode, output, false);
        }
    }
}