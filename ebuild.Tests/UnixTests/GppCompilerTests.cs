using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Compilers;
using ebuild.Linkers;
using System.Runtime.InteropServices;

namespace ebuild.Tests.UnixTests;

[TestFixture]
public class GppCompilerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Ignore();
        // Register platforms and compilers
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            CompilerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
        }
        catch (System.ArgumentException)
        {
            // Already registered, ignore
        }
    }

    [Test]
    public void GppCompiler_Should_Have_Correct_Name()
    {
        // Arrange
        var compiler = new GppCompiler();
        
        // Act
        var name = compiler.GetName();
        
        // Assert
        Assert.That(name, Is.EqualTo("Gpp"));
    }

    [Test]
    public void GppCompiler_Should_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var compiler = new GppCompiler();
        var platform = new UnixPlatform();
        
        // Act
        var isAvailable = compiler.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
    }

    [Test]
    public void GppCompiler_Should_Not_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var compiler = new GppCompiler();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = compiler.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void GppCompiler_Should_Be_Registered_In_Registry()
    {
        // Arrange
        var registry = CompilerRegistry.GetInstance();
        
        // Act & Assert
        Assert.DoesNotThrow(() => registry.Create<GppCompiler>());
    }

    [Test]
    public void GppCompiler_Should_Use_GccLinker_As_Default()
    {
        // Arrange
        var compiler = new GppCompiler();
        
        // Act
        var defaultLinker = compiler.GetDefaultLinker();
        
        // Assert
        Assert.That(defaultLinker, Is.InstanceOf<GccLinker>());
    }
}
