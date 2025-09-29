using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ebuild.BuildGraph;
using ebuild.api.Compiler;
using ebuild.api;

namespace ebuild.Tests.Integration;

[TestFixture]
[Order(3)]
public class CompilationSkippingTests
{
    private string _testModulePath = string.Empty;
    private string _testOutputDir = string.Empty;
    private string _ebuildExePath = string.Empty;
    private string _testSourceFile = string.Empty;
    private string _testHeaderFile = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _testOutputDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "compilation_skipping");
        var thisAssemblyLocation = Assembly.GetAssembly(GetType())!.Location;
        _ebuildExePath = Path.Combine(Path.GetDirectoryName(thisAssemblyLocation)!, "ebuild.dll");
        
        // Create test module and source files
        SetupTestFiles();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            if (Directory.Exists(_testOutputDir))
                Directory.Delete(_testOutputDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void SetupTestFiles()
    {
        Directory.CreateDirectory(_testOutputDir);

        // Create a simple C++ source file
        _testSourceFile = Path.Combine(_testOutputDir, "test.cpp");
        File.WriteAllText(_testSourceFile, @"
#include ""test.h""
#include <iostream>

int main() {
    std::cout << ""Hello "" << TEST_VALUE << std::endl;
    return 0;
}
");

        // Create a header file
        _testHeaderFile = Path.Combine(_testOutputDir, "test.h");
        File.WriteAllText(_testHeaderFile, @"
#ifndef TEST_H
#define TEST_H
#define TEST_VALUE ""World""
#endif
");

        // Create test module file
        _testModulePath = Path.Combine(_testOutputDir, "TestModule.ebuild.cs");
        File.WriteAllText(_testModulePath, $@"
using ebuild.api;

public class TestModule : ModuleBase
{{
    public TestModule(ModuleContext context) : base(context)
    {{
        Name = ""TestModule"";
        Type = ModuleType.Executable;
        
        SourceFiles.Add(""{_testSourceFile.Replace("\\", "\\\\")}"");
        Includes.Public.Add(""{_testOutputDir.Replace("\\", "\\\\")}"");
        
        Definitions.Public.Add(""TEST_DEFINE=1"");
    }}
}}
");
    }

    [Test]
    public void FirstBuild_ShouldCompileAllFiles()
    {
        // Arrange - Clean any existing build artifacts
        CleanBuildDirectory();

        // Act
        var result = RunEBuildCommand($"build \"{_testModulePath}\"");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0), $"Build failed: {result.Output}");
        Assert.That(result.Output, Does.Contain("Compiling"), "Expected compilation output");
        
        // Verify object files were created
        var objectFiles = Directory.GetFiles(_testOutputDir, "*.o", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_testOutputDir, "*.obj", SearchOption.AllDirectories))
            .ToArray();
        Assert.That(objectFiles.Length, Is.GreaterThan(0), "Expected object files to be created");
    }

    [Test]
    public void SecondBuild_ShouldSkipUnchangedFiles()
    {
        // Arrange - Ensure first build completed
        FirstBuild_ShouldCompileAllFiles();

        // Act
        var result = RunEBuildCommand($"build \"{_testModulePath}\"");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0), $"Build failed: {result.Output}");
        
        // Should not see compilation messages for unchanged files
        Assert.That(result.Output, Does.Not.Contain("Compiling"), 
            "Expected no compilation output for unchanged files");
    }

    [Test]
    public void ModifiedSourceFile_ShouldRecompile()
    {
        // Arrange - Ensure first build completed
        FirstBuild_ShouldCompileAllFiles();
        
        // Modify the source file
        Thread.Sleep(1100); // Ensure file timestamp is different
        File.WriteAllText(_testSourceFile, @"
#include ""test.h""
#include <iostream>

int main() {
    std::cout << ""Modified "" << TEST_VALUE << std::endl;
    return 0;
}
");

        // Act
        var result = RunEBuildCommand($"build \"{_testModulePath}\"");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0), $"Build failed: {result.Output}");
        Assert.That(result.Output, Does.Contain("Source file modified"), 
            "Expected compilation due to source file modification");
    }

    [Test]
    public void ModifiedHeaderFile_ShouldRecompile()
    {
        // Arrange - Ensure first build completed
        FirstBuild_ShouldCompileAllFiles();
        
        // Modify the header file
        Thread.Sleep(1100); // Ensure file timestamp is different
        File.WriteAllText(_testHeaderFile, @"
#ifndef TEST_H
#define TEST_H
#define TEST_VALUE ""Modified World""
#endif
");

        // Act
        var result = RunEBuildCommand($"build \"{_testModulePath}\"");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0), $"Build failed: {result.Output}");
        Assert.That(result.Output, Does.Contain("Dependency file modified"), 
            "Expected compilation due to dependency modification");
    }

    [Test]
    public void ModifiedDefinitions_ShouldRecompile()
    {
        // Arrange - Ensure first build completed
        FirstBuild_ShouldCompileAllFiles();
        
        // Modify the module to change definitions
        File.WriteAllText(_testModulePath, $@"
using ebuild.api;

public class TestModule : ModuleBase
{{
    public TestModule(ModuleContext context) : base(context)
    {{
        Name = ""TestModule"";
        Type = ModuleType.Executable;
        
        SourceFiles.Add(""{_testSourceFile.Replace("\\", "\\\\")}"");
        Includes.Public.Add(""{_testOutputDir.Replace("\\", "\\\\")}"");
        
        Definitions.Public.Add(""TEST_DEFINE=2""); // Changed value
    }}
}}
");

        // Act
        var result = RunEBuildCommand($"build \"{_testModulePath}\"");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0), $"Build failed: {result.Output}");
        Assert.That(result.Output, Does.Contain("Definitions have changed"), 
            "Expected compilation due to definition changes");
    }

    [Test]
    public void MissingOutputFile_ShouldRecompile()
    {
        // Arrange - Ensure first build completed
        FirstBuild_ShouldCompileAllFiles();
        
        // Delete object file
        var objectFiles = Directory.GetFiles(_testOutputDir, "*.o", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_testOutputDir, "*.obj", SearchOption.AllDirectories))
            .ToArray();
        foreach (var file in objectFiles)
        {
            File.Delete(file);
        }

        // Act
        var result = RunEBuildCommand($"build \"{_testModulePath}\"");

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0), $"Build failed: {result.Output}");
        Assert.That(result.Output, Does.Contain("Output file").And.Contain("not found"), 
            "Expected compilation due to missing output file");
    }

    private void CleanBuildDirectory()
    {
        try
        {
            var binariesDir = Path.Combine(_testOutputDir, "Binaries");
            if (Directory.Exists(binariesDir))
                Directory.Delete(binariesDir, true);

            var ebuildDir = Path.Combine(_testOutputDir, ".ebuild");
            if (Directory.Exists(ebuildDir))
                Directory.Delete(ebuildDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private (int ExitCode, string Output) RunEBuildCommand(string arguments, int timeoutSeconds = 60)
    {
        using var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = $"\"{_ebuildExePath}\" {arguments}";
        process.StartInfo.WorkingDirectory = _testOutputDir;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = process.WaitForExit(timeoutSeconds * 1000);
        if (!completed)
        {
            process.Kill();
            return (-1, "Process timed out");
        }
        
        Thread.Sleep(100); // Ensure all output is flushed
        var output = outputBuilder.ToString() + errorBuilder.ToString();
        return (process.ExitCode, output);
    }
}