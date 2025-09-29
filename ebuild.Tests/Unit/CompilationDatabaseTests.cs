using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ebuild.BuildGraph;
using ebuild.api;
using ebuild.api.Compiler;

namespace ebuild.Tests.Unit;

[TestFixture]
public class CompilationDatabaseTests
{
    private string _testDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "compilation_database", Guid.NewGuid().ToString());
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
    public void GetEntry_WhenFileDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var database = new CompilationDatabase(_testDir, "TestModule", "test.cpp");

        // Act
        var entry = database.GetEntry();

        // Assert
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void SaveAndGetEntry_ShouldPersistData()
    {
        // Arrange
        var database = new CompilationDatabase(_testDir, "TestModule", "test.cpp");
        var originalEntry = new CompilationEntry
        {
            SourceFile = "test.cpp",
            OutputFile = "test.obj",
            LastCompiled = DateTime.UtcNow,
            Definitions = new List<string> { "DEBUG=1", "VERSION=2" },
            IncludePaths = new List<string> { "/include1", "/include2" },
            ForceIncludes = new List<string> { "force1.h", "force2.h" },
            Dependencies = new List<string> { "dep1.h", "dep2.h" }
        };

        // Act
        database.SaveEntry(originalEntry);
        var retrievedEntry = database.GetEntry();

        // Assert
        Assert.That(retrievedEntry, Is.Not.Null);
        Assert.That(retrievedEntry!.SourceFile, Is.EqualTo(originalEntry.SourceFile));
        Assert.That(retrievedEntry!.OutputFile, Is.EqualTo(originalEntry.OutputFile));
        Assert.That(retrievedEntry!.Definitions, Is.EqualTo(originalEntry.Definitions));
        Assert.That(retrievedEntry!.IncludePaths, Is.EqualTo(originalEntry.IncludePaths));
        Assert.That(retrievedEntry!.ForceIncludes, Is.EqualTo(originalEntry.ForceIncludes));
        Assert.That(retrievedEntry!.Dependencies, Is.EqualTo(originalEntry.Dependencies));
    }

    [Test]
    public void CreateFromSettings_ShouldCreateValidEntry()
    {
        // Arrange
        var settings = new CompilerSettings
        {
            SourceFile = "test.cpp",
            OutputFile = "test.obj",
            TargetArchitecture = Architecture.X64,
            ModuleType = ModuleType.Executable,
            IntermediateDir = "/tmp",
            CppStandard = CppStandards.Cpp20,
            Definitions = new List<Definition> { "DEBUG=1", "VERSION=2" },
            IncludePaths = new List<string> { "/include1", "/include2" },
            ForceIncludes = new List<string> { "force1.h", "force2.h" }
        };

        // Act
        var entry = CompilationDatabase.CreateFromSettings(settings, "test.obj");

        // Assert
        Assert.That(entry.SourceFile, Is.EqualTo(settings.SourceFile));
        Assert.That(entry.OutputFile, Is.EqualTo("test.obj"));
        Assert.That(entry.Definitions, Contains.Item("DEBUG=1"));
        Assert.That(entry.Definitions, Contains.Item("VERSION=2"));
        Assert.That(entry.IncludePaths, Is.EqualTo(settings.IncludePaths));
        Assert.That(entry.ForceIncludes, Is.EqualTo(settings.ForceIncludes));
        Assert.That(entry.Dependencies, Is.Empty);
    }

    [Test]
    public void GetEntry_WithCorruptedFile_ShouldReturnNull()
    {
        // Arrange
        var database = new CompilationDatabase(_testDir, "TestModule", "test.cpp");
        
        // Create corrupted file
        var dbDir = Path.Combine(_testDir, ".ebuild", "TestModule");
        Directory.CreateDirectory(dbDir);
        var dbFile = Path.Combine(dbDir, "test.compile.json");
        File.WriteAllText(dbFile, "corrupted json content");

        // Act
        var entry = database.GetEntry();

        // Assert
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void SaveEntry_WithIOError_ShouldNotThrow()
    {
        // Arrange
        var database = new CompilationDatabase("/invalid/path", "TestModule", "test.cpp");
        var entry = new CompilationEntry
        {
            SourceFile = "test.cpp",
            OutputFile = "test.obj"
        };

        // Act & Assert
        Assert.DoesNotThrow(() => database.SaveEntry(entry));
    }
}