using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Linkers;
using ebuild.Compilers;
using System;

namespace ebuild.Tests.Common;

[TestFixture]
public class RegistryIntegrationTests
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
        }
        catch (System.ArgumentException)
        {
            // Already registered, ignore
        }
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

    [Test]
    public void PlatformRegistry_Should_Get_Host_Platform()
    {
        // Act
        var hostPlatform = PlatformRegistry.GetHostPlatform();
        
        // Assert
        Assert.That(hostPlatform, Is.Not.Null);
        Assert.That(hostPlatform.GetName(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void CompilerRegistry_Should_Get_Default_Compiler_Name()
    {
        // Act
        var defaultCompilerName = CompilerRegistry.GetDefaultCompilerName();
        
        // Assert
        Assert.That(defaultCompilerName, Is.Not.Null.And.Not.Empty);
    }
}
