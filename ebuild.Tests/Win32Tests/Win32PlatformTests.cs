using NUnit.Framework;
using ebuild.Platforms;
using System.Runtime.InteropServices;

namespace ebuild.Tests.Win32Tests;

[TestFixture]
public class Win32PlatformTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
    public void Win32Platform_Should_Be_Registered()
    {
        // Arrange
        var registry = PlatformRegistry.GetInstance();
        
        // Act & Assert
        Assert.DoesNotThrow(() => registry.Get<Win32Platform>());
    }

    [Test]
    public void Win32Platform_Should_Have_Correct_Name()
    {
        // Arrange
        var platform = new Win32Platform();
        
        // Act
        var name = platform.GetName();
        
        // Assert
        Assert.That(name, Is.EqualTo("Win32"));
    }

    [Test]
    public void Win32Platform_Should_Return_Msvc_As_Default_Compiler()
    {
        // Arrange
        var platform = new Win32Platform();
        
        // Act
        var compilerName = platform.GetDefaultCompilerName();
        
        // Assert
        Assert.That(compilerName, Is.EqualTo("Msvc"));
    }
}
