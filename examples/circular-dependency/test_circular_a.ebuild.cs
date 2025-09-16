using ebuild.api;

/// <summary>
/// Test module A that creates a circular dependency with module B.
/// This example demonstrates how the build graph detects circular dependencies.
/// A -> B -> A (circular)
/// </summary>
public class TestModuleA : ModuleBase
{
    public TestModuleA(ModuleContext context) : base(context)
    {
        Name = "TestModuleA";
        Type = ModuleType.StaticLibrary;
        
        // Create circular dependency: A -> B
        Dependencies.Public.Add(new ModuleReference("test_circular_b.ebuild.cs"));
    }
}