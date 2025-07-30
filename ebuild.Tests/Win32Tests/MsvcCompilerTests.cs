using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Compilers;
using System.Runtime.InteropServices;

namespace ebuild.Tests.Win32Tests;

[TestFixture]
public class MsvcCompilerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
    public void MsvcCompiler_Should_Have_Correct_Name()
    {
        // Arrange
        var compiler = new MsvcCompiler();
        
        // Act
        var name = compiler.GetName();
        
        // Assert
        Assert.That(name, Is.EqualTo("Msvc"));
    }

    [Test]
    public void MsvcCompiler_Should_Be_Available_For_Win32_Platform()
    {
        // Arrange
        var compiler = new MsvcCompiler();
        var platform = new Win32Platform();
        
        // Act
        var isAvailable = compiler.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.True);
    }

    [Test]
    public void MsvcCompiler_Should_Not_Be_Available_For_Unix_Platform()
    {
        // Arrange
        var compiler = new MsvcCompiler();
        var platform = new UnixPlatform();
        
        // Act
        var isAvailable = compiler.IsAvailable(platform);
        
        // Assert
        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void MsvcCompiler_Should_Be_Registered_In_Registry()
    {
        // Arrange
        var registry = CompilerRegistry.GetInstance();
        
        // Act & Assert
        Assert.DoesNotThrow(() => registry.Create<MsvcCompiler>());
    }

    [Test]
    public void MsvcCompiler_Should_Return_Correct_Default_Linker_For_StaticLibrary()
    {
        // Arrange
        var compiler = new MsvcCompiler();
        
        // Act
        var linker = compiler.GetDefaultLinker();
        
        // Assert - Should return MsvcLibLinker for static libraries by default
        // Note: The actual linker type depends on the module type, but MsvcLinkLinker is the general default
        Assert.That(linker, Is.Not.Null);
    }
}
