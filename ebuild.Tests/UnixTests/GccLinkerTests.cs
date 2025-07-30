using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Linkers;
using ebuild.Compilers;
using System;
using System.Runtime.InteropServices;

namespace ebuild.Tests.UnixTests;

[TestFixture]
public class GccLinkerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Ignore();
        // Register platforms, compilers, and linkers
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            LinkerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);

            // Also try to register explicitly
            LinkerRegistry.GetInstance().Register("Gcc", typeof(GccLinker));
        }
        catch (System.ArgumentException)
        {
            // Already registered, ignore
        }
    }

    [Test]
    public void GccLinker_Should_Have_Correct_Name()
    {
        // Arrange
        var linker = new GccLinker();

        // Act
        var name = linker.GetName();

        // Assert
        Assert.That(name, Is.EqualTo("Gcc"));
    }

    [Test]
    public void GccLinker_Should_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var linker = new GccLinker();
        var platform = new UnixPlatform();

        // On Unix systems, GCC should be available
        // Act
        var isAvailable = linker.IsAvailable(platform);

        // Assert
        Assert.That(isAvailable, Is.True, "GCC linker should be available on Unix systems");
    }

    [Test]
    public void GccLinker_Should_Not_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var linker = new GccLinker();
        var platform = new Win32Platform();

        // Act
        var isAvailable = linker.IsAvailable(platform);

        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void LinkerRegistry_Should_Register_And_Retrieve_GccLinker()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();

        // Act & Assert - Check if already registered or register manually
        try
        {
            registry.Register("Gcc", typeof(GccLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }

        Assert.DoesNotThrow(() => registry.Get<GccLinker>());
    }

    [Test]
    public void LinkerRegistry_Should_Retrieve_GccLinker_By_Name()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        try
        {
            registry.Register("Gcc", typeof(GccLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }

        // Act
        var gccLinker = registry.Get("Gcc");

        // Assert
        Assert.That(gccLinker, Is.InstanceOf<GccLinker>());
    }

    [Test]
    public void Compiler_Should_Accept_Linker_And_Pass_Module()
    {
        // Arrange
        var compiler = new GccCompiler();
        var linker = new GccLinker();

        // Act
        compiler.SetLinker(linker);

        // Assert - No exception should be thrown
        Assert.DoesNotThrow(() => compiler.SetLinker(linker));
    }
}
