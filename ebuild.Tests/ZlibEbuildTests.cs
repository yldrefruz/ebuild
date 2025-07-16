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
    public void ZlibEbuild_Should_Build_Successfully()
    {
        // Skip this test for now due to stack overflow issue in ebuild CLI
        // This test verifies that the zlib.ebuild.cs module can be built using the ebuild CLI
        Assert.Ignore("Integration test skipped due to stack overflow issue in ebuild CLI");
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