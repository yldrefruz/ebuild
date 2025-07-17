using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ebuild.api;
using ebuild.Platforms;
using ebuild.Compilers;
using ebuild.Linkers;

namespace ebuild.Tests;

[TestFixture]
public class ZlibEbuildTests
{
    private string _zlibModulePath;
    private string _testOutputDir;
    private string _ebuildExePath;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Register platforms, compilers, and linkers
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            CompilerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            LinkerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
        }
        catch (ArgumentException)
        {
            // Already registered, ignore
        }
        
        _zlibModulePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "zlib", "zlib.ebuild.cs");
        _testOutputDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "zlib");
        _ebuildExePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "bin", "Debug", "net8.0", "ebuild.dll");
        
        Directory.CreateDirectory(_testOutputDir);
    }

    [Test]
    public void ZlibEbuild_File_Should_Exist()
    {
        // Act & Assert
        Assert.That(File.Exists(_zlibModulePath), Is.True, $"zlib.ebuild.cs should exist at {_zlibModulePath}");
    }

    [Test]
    public async Task ZlibEbuild_Should_Build_Successfully()
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
            Arguments = $"run --project \"{Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ebuild", "ebuild.csproj")}\" -- build zlib.ebuild.cs",
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
        
        // The build should not result in a stack overflow or other critical error
        Assert.That(error, Does.Not.Contain("StackOverflowException"), "Should not have stack overflow");
        Assert.That(error, Does.Not.Contain("stack overflow"), "Should not have stack overflow");
        
        // The process should exit cleanly (exit code 0 indicates success)
        Assert.That(process.ExitCode, Is.EqualTo(0), $"Build should succeed. Exit code: {process.ExitCode}, Error: {error}");
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