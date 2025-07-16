# Linker Extraction Example

This example demonstrates how to use the new separated linker system:

## Basic Usage

```csharp
// Create a compiler
var compiler = new GccCompiler();
await compiler.Setup();

// Create a linker separately
var linker = new GccLinker();
await linker.Setup();

// Set up the compiler with the linker
compiler.SetLinker(linker);

// Now compilation will use the separate linker
await compiler.Compile(); // This will compile and then link using the separate linker
```

## Target-based Linking

The new structure allows for target-based linking where static libraries can be linked directly to the target:

```csharp
// Create multiple modules
var module1 = new StaticLibraryModule();
var module2 = new StaticLibraryModule();
var executableModule = new ExecutableModule();

// Compile modules separately (compilation only)
var compiler = new GccCompiler();
await compiler.Setup();

compiler.SetModule(module1);
await compiler.Compile(); // Compiles to object files only

compiler.SetModule(module2);
await compiler.Compile(); // Compiles to object files only

// Link everything together at the target level
var linker = new GccLinker();
await linker.Setup();

linker.SetModule(executableModule);
// The linker can now link all static libraries directly to the executable
// instead of having them as intermediate dynamic libraries
await linker.Link();
```

## Registry Usage

```csharp
// Register linkers
LinkerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);

// Get linker by name
var gccLinker = LinkerRegistry.GetInstance().Get("Gcc");
var msvcLinker = LinkerRegistry.GetInstance().Get("Msvc");

// Get linker by type
var gccLinker2 = LinkerRegistry.GetInstance().Get<GccLinker>();
```

This design enables the UBT target-like system where targets can manage linking independently of compilation.