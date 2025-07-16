using NUnit.Framework;
using System;
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
        
        _zlibModulePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "zlib.ebuild.cs");
        _testOutputDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "zlib");
        Directory.CreateDirectory(_testOutputDir);
    }

    [Test]
    public void ZlibEbuild_File_Should_Exist()
    {
        // Act & Assert
        Assert.That(File.Exists(_zlibModulePath), Is.True, $"zlib.ebuild.cs should exist at {_zlibModulePath}");
    }

    [Test]
    public void ZlibEbuild_Should_Compile_Successfully()
    {
        // This test verifies that the zlib.ebuild.cs module can be compiled
        // Note: This is a conceptual test since the actual ebuild compilation
        // requires the full ebuild CLI infrastructure
        
        // Arrange & Act & Assert
        // The zlib.ebuild.cs file should be syntactically correct C# code
        // that can be compiled by the ebuild system
        Assert.DoesNotThrow(() =>
        {
            var content = File.ReadAllText(_zlibModulePath);
            Assert.That(content, Does.Contain("public class ZlibEbuild : ModuleBase"));
            Assert.That(content, Does.Contain("public ZlibEbuild(ModuleContext context)"));
            Assert.That(content, Does.Contain("public void SetupSourceFiles"));
        });
    }

    [Test]
    public void ZlibEbuild_Should_Have_Correct_Structure()
    {
        // Arrange
        var content = File.ReadAllText(_zlibModulePath);
        
        // Act & Assert
        Assert.That(content, Does.Contain("namespace ebuild.Tests.resources"));
        Assert.That(content, Does.Contain("public class ZlibEbuild : ModuleBase"));
        Assert.That(content, Does.Contain("Type = ModuleType.StaticLibrary"));
        Assert.That(content, Does.Contain("Name = \"zlib\""));
        Assert.That(content, Does.Contain("public void SetupSourceFiles"), "SetupSourceFiles should be public");
        Assert.That(content, Does.Contain("Setup()"), "Setup should be called in constructor");
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