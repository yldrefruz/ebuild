using NUnit.Framework;
using ebuild.Platforms;
using System.Runtime.InteropServices;

namespace ebuild.Tests.UnixTests;

[TestFixture]
public class UnixPlatformTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Skip this entire test fixture on Windows as it's Unix-specific
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Assert.Ignore();    
        // Register platforms
        try
        {
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
        }
        catch (System.ArgumentException)
        {
            // Already registered, ignore
        }
    }

    [Test]
    public void UnixPlatform_Should_Be_Registered()
    {
        // Arrange
        var registry = PlatformRegistry.GetInstance();
        
        // Act & Assert
        Assert.DoesNotThrow(() => registry.Get<UnixPlatform>());
    }

    [Test]
    public void UnixPlatform_Should_Have_Correct_Name()
    {
        // Arrange
        var platform = new UnixPlatform();
        
        // Act
        var name = platform.GetName();
        
        // Assert
        Assert.That(name, Is.EqualTo("Unix"));
    }

    [Test]
    public void UnixPlatform_Should_Return_Gcc_As_Default_Compiler()
    {
        // Arrange
        var platform = new UnixPlatform();
        
        // Act
        var compilerName = platform.GetDefaultCompilerName();
        
        // Assert
        Assert.That(compilerName, Is.EqualTo("Gcc"));
    }
}
