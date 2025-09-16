# Circular Dependency Example

This example demonstrates the circular dependency detection capability of the EBuild system.

## Files

- `test_circular_a.ebuild.cs` - Module A that depends on Module B
- `test_circular_b.ebuild.cs` - Module B that depends on Module A

## Circular Dependency Pattern

```
TestModuleA -> TestModuleB -> TestModuleA (circular)
```

## Testing Circular Dependency Detection

To test the circular dependency detection:

```bash
# This should detect and report the circular dependency
ebuild check circular-dependencies examples/circular-dependency/test_circular_a.ebuild.cs
```

Expected output:
```
Circular dependency detected in examples/circular-dependency/test_circular_a.ebuild.cs
Circular dependency detected:
  TestModuleA -> TestModuleB -> TestModuleA -> TestModuleA
```

## How It Works

The build graph system:
1. Prevents infinite recursion during graph construction by tracking modules currently being constructed
2. Creates references to already-constructing modules to represent the circular dependency in the graph
3. Uses depth-first search to detect cycles in the dependency graph
4. Reports the exact path of modules forming the circular dependency

This ensures that circular dependencies are both safely handled during construction and properly detected and reported to the user.