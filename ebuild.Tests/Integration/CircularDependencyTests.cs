using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ebuild.Tests.Integration;

[TestFixture, Order(1)]
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
        var thisAssemblyLocation = Assembly.GetAssembly(GetType()).Location;
        _ebuildExePath = Path.Combine(Path.GetDirectoryName(thisAssemblyLocation)!, "ebuild.dll");

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

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) =>
        {
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
        Thread.Sleep(100); // Ensure all output is flushed
        var output = outputBuilder.ToString() + errorBuilder.ToString();
        return (process.ExitCode, output, false);
    }
}