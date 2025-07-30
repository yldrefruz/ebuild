using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Linkers;
using ebuild.Compilers;
using System;
using System.Runtime.InteropServices;

namespace ebuild.Tests.Win32Tests;

[TestFixture]
public class MsvcLinkerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Ignore();    
        // Register platforms, compilers, and linkers
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            LinkerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            
            // Also try to register explicitly
            LinkerRegistry.GetInstance().Register("MsvcLink", typeof(MsvcLinkLinker));
            LinkerRegistry.GetInstance().Register("MsvcLib", typeof(MsvcLibLinker));
        }
        catch (System.ArgumentException)
        {
            // Already registered, ignore
        }
    }

    [Test]
    public void MsvcLinkLinker_Should_Have_Correct_Name()
    {
        // Arrange
        var linker = new MsvcLinkLinker();

        // Act
        var name = linker.GetName();

        // Assert
        Assert.That(name, Is.EqualTo("MsvcLink"));
    }

    [Test]
    public void MsvcLibLinker_Should_Have_Correct_Name()
    {
        // Arrange
        var linker = new MsvcLibLinker();

        // Act
        var name = linker.GetName();

        // Assert
        Assert.That(name, Is.EqualTo("MsvcLib"));
    }

    [Test]
    public void MsvcLinkLinker_Should_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var linker = new MsvcLinkLinker();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
    }

    [Test]
    public void MsvcLinkLinker_Should_Not_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var linker = new MsvcLinkLinker();
        var platform = new UnixPlatform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void MsvcLibLinker_Should_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var linker = new MsvcLibLinker();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
    }

    [Test]
    public void MsvcLibLinker_Should_Not_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var linker = new MsvcLibLinker();
        var platform = new UnixPlatform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void LinkerRegistry_Should_Register_And_Retrieve_MsvcLinkLinker()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        
        // Act & Assert - Check if already registered or register manually
        try
        {
            registry.Register("MsvcLink", typeof(MsvcLinkLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        Assert.DoesNotThrow(() => registry.Get<MsvcLinkLinker>());
    }

    [Test]
    public void LinkerRegistry_Should_Register_And_Retrieve_MsvcLibLinker()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        
        // Act & Assert - Check if already registered or register manually
        try
        {
            registry.Register("MsvcLib", typeof(MsvcLibLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        Assert.DoesNotThrow(() => registry.Get<MsvcLibLinker>());
    }

    [Test]
    public void LinkerRegistry_Should_Retrieve_MsvcLinkers_By_Name()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        try
        {
            registry.Register("MsvcLink", typeof(MsvcLinkLinker));
            registry.Register("MsvcLib", typeof(MsvcLibLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        // Act
        var msvcLinkLinker = registry.Get("MsvcLink");
        var msvcLibLinker = registry.Get("MsvcLib");
        
        // Assert
        Assert.That(msvcLinkLinker, Is.InstanceOf<MsvcLinkLinker>());
        Assert.That(msvcLibLinker, Is.InstanceOf<MsvcLibLinker>());
    }

    [Test]
    public void Compiler_Should_Accept_Linker_And_Pass_Module()
    {
        // Arrange
        var compiler = new MsvcCompiler();
        var linker = new MsvcLinkLinker();
        
        // Act
        compiler.SetLinker(linker);
        
        // Assert - No exception should be thrown
        Assert.DoesNotThrow(() => compiler.SetLinker(linker));
    }
}
