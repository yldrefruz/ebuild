using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Linkers;
using System;
using System.Runtime.InteropServices;

namespace ebuild.Tests.UnixTests;

[TestFixture]
public class ArLinkerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Register platforms, compilers, and linkers
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            LinkerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            
            // Also try to register explicitly
            LinkerRegistry.GetInstance().Register("Ar", typeof(ArLinker));
        }
        catch (System.ArgumentException)
        {
            // Already registered, ignore
        }
    }

    [Test]
    public void ArLinker_Should_Have_Correct_Name()
    {
        // Arrange
        var linker = new ArLinker();

        // Act
        var name = linker.GetName();

        // Assert
        Assert.That(name, Is.EqualTo("Ar"));
    }

    [Test]
    public void ArLinker_Should_Be_Available_For_Unix_Platform()
    {
        // Skip this test on Windows as it's Unix-specific
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Ignore();
        // Arrange
        var linker = new ArLinker();
        var platform = new UnixPlatform();
        
        // On Unix systems, AR should be available
        // Act
        var isAvailable = linker.IsAvailable(platform);
    
        // Assert
        Assert.That(isAvailable, Is.True, "AR linker should be available on Unix systems");
    }

    [Test]
    public void ArLinker_Should_Not_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var linker = new ArLinker();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void LinkerRegistry_Should_Register_And_Retrieve_ArLinker()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        
        // Act & Assert - Check if already registered or register manually
        try
        {
            registry.Register("Ar", typeof(ArLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        Assert.DoesNotThrow(() => registry.Get<ArLinker>());
    }

    [Test]
    public void LinkerRegistry_Should_Retrieve_ArLinker_By_Name()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        try
        {
            registry.Register("Ar", typeof(ArLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        // Act
        var arLinker = registry.Get("Ar");
        
        // Assert
        Assert.That(arLinker, Is.InstanceOf<ArLinker>());
    }
}
