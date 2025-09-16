using ebuild.api;

/// <summary>
/// Test module B that creates a circular dependency with module A.
/// This example demonstrates how the build graph detects circular dependencies.
/// B -> A -> B (circular)
/// </summary>
public class TestModuleB : ModuleBase
{
    public TestModuleB(ModuleContext context) : base(context)
    {
        Name = "TestModuleB";
        Type = ModuleType.StaticLibrary;
        
        // Create circular dependency: B -> A
        Dependencies.Public.Add(new ModuleReference("test_circular_a.ebuild.cs"));
    }
}