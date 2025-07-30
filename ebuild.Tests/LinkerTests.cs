using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Linkers;
using ebuild.Compilers;
using System;

namespace ebuild.Tests;

[TestFixture]
public class LinkerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Register platforms, compilers, and linkers
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            CompilerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            LinkerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            
            // Also try to register explicitly
            LinkerRegistry.GetInstance().Register("Gcc", typeof(GccLinker));
            LinkerRegistry.GetInstance().Register("MsvcLink", typeof(MsvcLinkLinker));
            LinkerRegistry.GetInstance().Register("MsvcLib", typeof(MsvcLibLinker));
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
    public void GccLinker_Should_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var linker = new GccLinker();
        var platform = new UnixPlatform();
        
        if(PlatformRegistry.GetHostPlatform().GetType() == typeof(Win32Platform)){
            // On Windows, GCC linker typically won't be available for Unix platform
            // unless specifically installed (like MinGW)
            // Act
            var isAvailable = linker.IsAvailable(platform);
        
            // Assert - On Windows, we don't expect GCC to be available by default
            Assert.That(isAvailable, Is.False, "GCC linker should not be available on Windows without MinGW");
        }
        else
        {
            // On Unix systems, GCC should be available
            // Act
            var isAvailable = linker.IsAvailable(platform);
        
            // Assert
            Assert.That(isAvailable, Is.True, "GCC linker should be available on Unix systems");
        }
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
    public void LinkerRegistry_Should_Retrieve_Linker_By_Name()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        try
        {
            registry.Register("Gcc", typeof(GccLinker));
            registry.Register("MsvcLink", typeof(MsvcLinkLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        // Act
        var gccLinker = registry.Get("Gcc");
        var msvcLinker = registry.Get("MsvcLink");
        
        // Assert
        Assert.That(gccLinker, Is.InstanceOf<GccLinker>());
        Assert.That(msvcLinker, Is.InstanceOf<MsvcLinkLinker>());
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