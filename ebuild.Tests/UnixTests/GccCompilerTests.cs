using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Compilers;
using System.Runtime.InteropServices;

namespace ebuild.Tests.UnixTests;

[TestFixture]
public class GccCompilerTests
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
    public void GccCompiler_Should_Have_Correct_Name()
    {
        // Arrange
        var compiler = new GccCompiler();
        
        // Act
        var name = compiler.GetName();
        
        // Assert
        Assert.That(name, Is.EqualTo("Gcc"));
    }

    [Test]
    public void GccCompiler_Should_Be_Available_For_Unix_Platform()
    {
        // Skip this test on Windows as it's Unix-specific
        Assume.That(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), Is.False, 
            "This test is Unix-specific and should be skipped on Windows");
            
        // Arrange
        var compiler = new GccCompiler();
        var platform = new UnixPlatform();
        
        // Act
        var isAvailable = compiler.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
    }

    [Test]
    public void GccCompiler_Should_Not_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var compiler = new GccCompiler();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = compiler.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void GccCompiler_Should_Be_Registered_In_Registry()
    {
        // Arrange
        var registry = CompilerRegistry.GetInstance();
        
        // Act & Assert
        Assert.DoesNotThrow(() => registry.Create<GccCompiler>());
    }
}
