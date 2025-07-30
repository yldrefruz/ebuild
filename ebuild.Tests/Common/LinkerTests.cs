using NUnit.Framework;
using ebuild.Platforms;
using ebuild.Linkers;
using ebuild.Compilers;
using System;

namespace ebuild.Tests.Common;

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
}