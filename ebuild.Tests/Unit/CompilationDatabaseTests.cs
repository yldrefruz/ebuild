using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ebuild.Modules.BuildGraph;
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
        var database = CompilationDatabase.Get(_testDir, "TestModule", "test.cpp");

        // Act
        var entry = database.GetEntry();

        // Assert
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void SaveAndGetEntry_ShouldPersistData()
    {
        // Arrange
        var database = CompilationDatabase.Get(_testDir, "TestModule", "test.cpp");
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
    public void GetEntry_WithCorruptedDatabase_ShouldReturnNull()
    {
        // Arrange
        var dbDir = Path.Combine(_testDir, ".ebuild", "TestModule");
        Directory.CreateDirectory(dbDir);
        var dbFile = Path.Combine(dbDir, "compilation.db");
        
        // Create corrupted database file (not a valid SQLite database)
        File.WriteAllText(dbFile, "corrupted database content");

        var database = CompilationDatabase.Get(_testDir, "TestModule", "test.cpp");

        // Act
        var entry = database.GetEntry();

        // Assert - Should return null when database is corrupted
        Assert.That(entry, Is.Null);
    }

    [Test]
    public void SaveEntry_WithIOError_ShouldNotThrow()
    {
        // Arrange
        var database = CompilationDatabase.Get("/invalid/path", "TestModule", "test.cpp");
        var entry = new CompilationEntry
        {
            SourceFile = "test.cpp",
            OutputFile = "test.obj"
        };

        // Act & Assert
        Assert.DoesNotThrow(() => database.SaveEntry(entry));
    }

    [Test]
    public void RemoveEntry_WhenFileExists_ShouldRemoveFile()
    {
        // Arrange
        var database = CompilationDatabase.Get(_testDir, "TestModule", "test.cpp");
        var entry = new CompilationEntry
        {
            SourceFile = "test.cpp",
            OutputFile = "test.obj",
            LastCompiled = DateTime.UtcNow
        };

        // First save an entry
        database.SaveEntry(entry);
        Assert.That(database.GetEntry(), Is.Not.Null);

        // Act
        database.RemoveEntry();

        // Assert
        Assert.That(database.GetEntry(), Is.Null);
    }

    [Test]
    public void RemoveEntry_WhenFileDoesNotExist_ShouldNotThrow()
    {
        // Arrange
        var database = CompilationDatabase.Get(_testDir, "TestModule", "test.cpp");

        // Act & Assert
        Assert.DoesNotThrow(() => database.RemoveEntry());
    }

    [Test]
    public void RemoveEntry_WithInvalidPath_ShouldNotThrow()
    {
        // Arrange
        var database = CompilationDatabase.Get("/invalid/path", "TestModule", "test.cpp");

        // Act & Assert
        Assert.DoesNotThrow(() => database.RemoveEntry());
    }

    [Test]
    public void MultipleSourceFiles_ShouldUseSameDatabase()
    {
        // Arrange - Create entries for multiple source files in the same module
        var database1 = CompilationDatabase.Get(_testDir, "TestModule", "file1.cpp");
        var database2 = CompilationDatabase.Get(_testDir, "TestModule", "file2.cpp");

        var entry1 = new CompilationEntry
        {
            SourceFile = "file1.cpp",
            OutputFile = "file1.obj",
            LastCompiled = DateTime.UtcNow,
            Definitions = new List<string> { "FILE1" }
        };

        var entry2 = new CompilationEntry
        {
            SourceFile = "file2.cpp",
            OutputFile = "file2.obj",
            LastCompiled = DateTime.UtcNow,
            Definitions = new List<string> { "FILE2" }
        };

        // Act - Save both entries
        database1.SaveEntry(entry1);
        database2.SaveEntry(entry2);

        // Assert - Both entries should be retrievable
        var retrieved1 = database1.GetEntry();
        var retrieved2 = database2.GetEntry();

        Assert.That(retrieved1, Is.Not.Null);
        Assert.That(retrieved2, Is.Not.Null);
        Assert.That(retrieved1!.SourceFile, Is.EqualTo("file1.cpp"));
        Assert.That(retrieved2!.SourceFile, Is.EqualTo("file2.cpp"));
        Assert.That(retrieved1.Definitions, Contains.Item("FILE1"));
        Assert.That(retrieved2.Definitions, Contains.Item("FILE2"));

        // Verify they use the same database file
        var dbPath = Path.Combine(_testDir, ".ebuild", "TestModule", "compilation.db");
        Assert.That(File.Exists(dbPath), Is.True, "Database file should exist");
    }
}