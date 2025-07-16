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
            LinkerRegistry.GetInstance().Register("Msvc", typeof(MsvcLinker));
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
    public void MsvcLinker_Should_Have_Correct_Name()
    {
        // Arrange
        var linker = new MsvcLinker();
        
        // Act
        var name = linker.GetName();
        
        // Assert
        Assert.That(name, Is.EqualTo("Msvc"));
    }

    [Test]
    public void GccLinker_Should_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var linker = new GccLinker();
        var platform = new UnixPlatform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
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
    public void MsvcLinker_Should_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var linker = new MsvcLinker();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = linker.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
    }

    [Test]
    public void MsvcLinker_Should_Not_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var linker = new MsvcLinker();
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
    public void LinkerRegistry_Should_Register_And_Retrieve_MsvcLinker()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        
        // Act & Assert - Check if already registered or register manually
        try
        {
            registry.Register("Msvc", typeof(MsvcLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        Assert.DoesNotThrow(() => registry.Get<MsvcLinker>());
    }

    [Test]
    public void LinkerRegistry_Should_Retrieve_Linker_By_Name()
    {
        // Arrange
        var registry = LinkerRegistry.GetInstance();
        try
        {
            registry.Register("Gcc", typeof(GccLinker));
            registry.Register("Msvc", typeof(MsvcLinker));
        }
        catch (ArgumentException)
        {
            // Already registered, that's fine
        }
        
        // Act
        var gccLinker = registry.Get("Gcc");
        var msvcLinker = registry.Get("Msvc");
        
        // Assert
        Assert.That(gccLinker, Is.InstanceOf<GccLinker>());
        Assert.That(msvcLinker, Is.InstanceOf<MsvcLinker>());
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