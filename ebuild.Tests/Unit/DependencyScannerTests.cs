using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ebuild.BuildGraph;

namespace ebuild.Tests.Unit;

[TestFixture]
public class DependencyScannerTests
{
    private string _testDir;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "dependency_scanner", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    public void ScanDependencies_WithLocalIncludes_ShouldFindDependencies()
    {
        // Arrange
        var headerFile = Path.Combine(_testDir, "header.h");
        File.WriteAllText(headerFile, @"
#ifndef HEADER_H
#define HEADER_H
void function();
#endif
");

        var sourceFile = Path.Combine(_testDir, "source.cpp");
        File.WriteAllText(sourceFile, @"
#include ""header.h""
#include <iostream>

void function() {
    std::cout << ""Hello"" << std::endl;
}
");

        // Act
        var dependencies = DependencyScanner.ScanDependencies(sourceFile, new List<string> { _testDir });

        // Assert
        Assert.That(dependencies, Is.Not.Empty);
        Assert.That(dependencies, Does.Contain(Path.GetFullPath(headerFile)));
    }

    [Test]
    public void ScanDependencies_WithNestedIncludes_ShouldFindAllDependencies()
    {
        // Arrange
        var header2File = Path.Combine(_testDir, "header2.h");
        File.WriteAllText(header2File, @"
#ifndef HEADER2_H
#define HEADER2_H
void function2();
#endif
");

        var header1File = Path.Combine(_testDir, "header1.h");
        File.WriteAllText(header1File, $@"
#ifndef HEADER1_H
#define HEADER1_H
#include ""header2.h""
void function1();
#endif
");

        var sourceFile = Path.Combine(_testDir, "source.cpp");
        File.WriteAllText(sourceFile, @"
#include ""header1.h""
#include <iostream>

void function() {
    function1();
    function2();
}
");

        // Act
        var dependencies = DependencyScanner.ScanDependencies(sourceFile, new List<string> { _testDir });

        // Assert
        Assert.That(dependencies, Does.Contain(Path.GetFullPath(header1File)));
        Assert.That(dependencies, Does.Contain(Path.GetFullPath(header2File)));
    }

    [Test]
    public void ScanDependencies_WithCircularIncludes_ShouldNotHang()
    {
        // Arrange
        var header1File = Path.Combine(_testDir, "header1.h");
        var header2File = Path.Combine(_testDir, "header2.h");

        File.WriteAllText(header1File, @"
#ifndef HEADER1_H
#define HEADER1_H
#include ""header2.h""
void function1();
#endif
");

        File.WriteAllText(header2File, @"
#ifndef HEADER2_H
#define HEADER2_H
#include ""header1.h""
void function2();
#endif
");

        var sourceFile = Path.Combine(_testDir, "source.cpp");
        File.WriteAllText(sourceFile, @"
#include ""header1.h""

void function() {
    function1();
}
");

        // Act & Assert - Should not hang or throw
        Assert.DoesNotThrow(() =>
        {
            var dependencies = DependencyScanner.ScanDependencies(sourceFile, new List<string> { _testDir });
            Assert.That(dependencies, Is.Not.Empty);
        });
    }

    [Test]
    public void ScanDependencies_WithSystemIncludes_ShouldIgnoreSystemHeaders()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "source.cpp");
        File.WriteAllText(sourceFile, @"
#include <iostream>
#include <vector>
#include <string>

int main() {
    std::cout << ""Hello"" << std::endl;
    return 0;
}
");

        // Act
        var dependencies = DependencyScanner.ScanDependencies(sourceFile, new List<string> { _testDir });

        // Assert
        Assert.That(dependencies, Is.Empty, "System includes should be ignored");
    }

    [Test]
    public void GetLatestModificationTime_WithMultipleFiles_ShouldReturnLatest()
    {
        // Arrange
        var file1 = Path.Combine(_testDir, "file1.txt");
        var file2 = Path.Combine(_testDir, "file2.txt");
        var file3 = Path.Combine(_testDir, "file3.txt");

        File.WriteAllText(file1, "content1");
        Thread.Sleep(100);
        File.WriteAllText(file2, "content2");
        Thread.Sleep(100);
        File.WriteAllText(file3, "content3");

        var files = new[] { file1, file2, file3 };

        // Act
        var latestTime = DependencyScanner.GetLatestModificationTime(files);

        // Assert
        var file3Time = File.GetLastWriteTimeUtc(file3);
        Assert.That(latestTime, Is.EqualTo(file3Time).Within(TimeSpan.FromSeconds(1)));
    }
}