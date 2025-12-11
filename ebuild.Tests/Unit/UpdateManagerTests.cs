using NUnit.Framework;
using System;

namespace ebuild.Tests.Unit;

[TestFixture]
public class UpdateManagerTests
{
    [Test]
    public void GetCurrentVersion_ReturnsValidVersion()
    {
        // Act
        var version = ebuild.UpdateManager.GetCurrentVersion();

        // Assert
        Assert.That(version, Is.Not.Null);
        Assert.That(version.Major, Is.GreaterThanOrEqualTo(0));
        Assert.That(version.Minor, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void GetCurrentVersionString_ReturnsNonEmptyString()
    {
        // Act
        var versionString = ebuild.UpdateManager.GetCurrentVersionString();

        // Assert
        Assert.That(versionString, Is.Not.Null);
        Assert.That(versionString, Is.Not.Empty);
        // Version string should contain at least major.minor format
        Assert.That(versionString, Does.Match(@"\d+\.\d+"));
    }

    [Test]
    public void UpdateManager_CanBeConstructed()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => new ebuild.UpdateManager());
    }
}
