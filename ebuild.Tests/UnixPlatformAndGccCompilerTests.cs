using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Compilers;

namespace ebuild.Tests;

[TestFixture]
public class UnixPlatformTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Only register if not already registered
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

[TestFixture]
public class GccCompilerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Only register if not already registered
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
}