using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using ebuild.BuildGraph;
using ebuild.api;

namespace ebuild.Tests.Unit;

[TestFixture]
public class CompilationDatabaseFailureTests
{
    private string _testDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "compilation_failure", Guid.NewGuid().ToString());
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
    public void RemoveEntry_AfterFailedCompilation_ShouldCleanupProperly()
    {
        // This test demonstrates the key behavior that was missing:
        // When compilation fails, the database entry should be removed
        // so that subsequent builds will retry compilation

        // Arrange
        var sourceFile = Path.Combine(_testDir, "test.cpp");
        File.WriteAllText(sourceFile, "int main() { return 0; }");

        var database = new CompilationDatabase(_testDir, "TestModule", sourceFile);
        
        // Create an entry as if a previous compilation was successful
        var entry = new CompilationEntry
        {
            SourceFile = sourceFile,
            OutputFile = Path.Combine(_testDir, "test.obj"),
            LastCompiled = DateTime.UtcNow.AddMinutes(-1),
            Definitions = new List<string> { "DEBUG=1" },
            IncludePaths = new List<string> { "/usr/include" },
            ForceIncludes = new List<string>(),
            Dependencies = new List<string> { sourceFile }
        };

        database.SaveEntry(entry);
        Assert.That(database.GetEntry(), Is.Not.Null, "Entry should exist after saving");

        // Act - Simulate compilation failure cleanup
        database.RemoveEntry();

        // Assert - Entry should be gone so subsequent builds will retry
        Assert.That(database.GetEntry(), Is.Null, "Entry should be removed after compilation failure");
        
        // Verify that saving and removing works multiple times
        database.SaveEntry(entry);
        Assert.That(database.GetEntry(), Is.Not.Null, "Entry should exist after re-saving");
        
        database.RemoveEntry();
        Assert.That(database.GetEntry(), Is.Null, "Entry should be removed again");
    }

    [Test]
    public void CompilationDatabase_BehaviorAfterFailure_DemonstratesIssue()
    {
        // This test demonstrates the behavior described in the issue:
        // Before the fix: Failed compilation leaves stale database entry
        // After the fix: Failed compilation removes database entry

        var sourceFile = Path.Combine(_testDir, "test.cpp");
        File.WriteAllText(sourceFile, "int main() { return 0; }");

        var database = new CompilationDatabase(_testDir, "TestModule", sourceFile);
        
        // Simulate: Initial successful compilation creates database entry
        var successEntry = new CompilationEntry
        {
            SourceFile = sourceFile,
            OutputFile = Path.Combine(_testDir, "test.obj"),
            LastCompiled = DateTime.UtcNow,
            Definitions = new List<string>(),
            IncludePaths = new List<string>(),
            ForceIncludes = new List<string>(),
            Dependencies = new List<string>()
        };
        database.SaveEntry(successEntry);
        
        // Verify entry exists
        Assert.That(database.GetEntry(), Is.Not.Null, "Database should have entry after successful compilation");
        
        // Simulate: Compilation failure - should remove entry (this is the fix)
        database.RemoveEntry();
        
        // Verify entry is gone - this ensures subsequent builds will retry compilation
        Assert.That(database.GetEntry(), Is.Null, "Database should not have entry after compilation failure");
    }
}