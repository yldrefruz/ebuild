# EBuild Copilot Instructions

## Architecture Overview

EBuild is a modular C++ build system with a .NET-based CLI. The project consists of three main components:

- **ebuild** - CLI application using CliFx framework for commands (`build`, `generate`, `check`, `property`)
- **ebuild.api** - Framework for defining modules, platforms, toolchains, and compilers 
- **ebuild.Tests** - NUnit-based test suite

## Module System Fundamentals

### Module Definition Pattern
Modules inherit from `ModuleBase` and are defined in `.ebuild.cs` files:

```csharp
public class MyModule : ModuleBase
{
    public MyModule(ModuleContext context) : base(context)
    {
        Name = "MyModule";
        Type = ModuleType.StaticLibrary; // or SharedLibrary, Executable, ExecutableWin32
        
        // Access-limited collections (Public/Private)
        Includes.Public.Add("include");
        Dependencies.Private.Add(new ModuleReference("SomeDependency"));
        Libraries.Public.Add("system_lib.lib");
        
        // Direct collections
        SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, "src", "cpp", "h"));
        CompilerOptions.Add("/W4");
    }
}
```

### Access Control Pattern
Most module properties use `AccessLimitList<T>` with `.Public` and `.Private` collections that are automatically propagated through the dependency tree. Use `.Joined()` to get combined results.

## Toolchain & Platform Architecture

### Platform Abstraction
- Platforms inherit from `PlatformBase` and define target-specific behavior
- Use attributes: `[Platform("MyPlatform")]` for auto-registration
- Key methods: `GetDefaultToolchainName()`, `GetPlatformDefinitions()`, `GetPlatformCompilerFlags()`

### Toolchain Pattern
- Toolchains implement `IToolchain` and create compiler/linker factories
- Two built-in toolchains: `GccToolchain`, `MSVCToolchain`
- Factory pattern: toolchain → factory → concrete compiler/linker instances

### Compiler/Linker Extension
```csharp
[Compiler("MyCompiler")]
public class MyCompiler : CompilerBase
{
    public override async Task<bool> Setup() { /* Setup logic */ }
    public override async Task<bool> Compile() { /* Compile with settings */ }
    public override string GetExecutablePath() { /* Return compiler path */ }
}
```

## Build Graph & Dependency System

### Dependency Resolution
- `BuildGraph` builds from module files and resolves circular dependencies with caching
- `ModuleDeclarationNode` creates build graph nodes with compile/link steps
- Access limits propagate: Public dependencies become transitive, Private stay local

### Build Artifacts
- Object files: `CompilerUtils.GetObjectOutputFolder(module)`
- Output binaries: `module.GetBinaryOutputPath()`
- Use `CompilerUtils.FindBuildArtifacts()` for cleanup operations

## Command Development Pattern

Commands inherit from `ModuleCreatingCommand` (for module-aware commands) or implement `ICommand`:

```csharp
[Command("mycommand", Description = "Does something")]
public class MyCommand : ModuleCreatingCommand
{
    [CommandOption("option", 'o', Description = "An option")]
    public bool SomeOption { get; init; } = false;
    
    public override async ValueTask ExecuteAsync(IConsole console)
    {
        // Access module via ModuleInstancingParams.SelfModuleReference
        // Directory operations should use absolute paths
    }
}
```

## Key Development Patterns

### Service Registration
Use `[AutoRegisterService(typeof(IInterface))]` attribute for dependency injection.

### Module Options
Use `[ModuleOption]` for build-affecting parameters:
```csharp
[ModuleOption(Description = "Enable feature", ChangesResultBinary = true)]
public bool EnableFeature = false;
```

### Logging
Access via `EBuild.LoggerFactory.CreateLogger("Category")` - integrated throughout the system.

## Testing Strategy

- Tests use NUnit with `GlobalTestSetup` for EBuild initialization
- Integration tests in `ebuild.Tests/Integration/` test real build scenarios
- Use `EBuild.DisableLogging = true` in tests to reduce noise

## Build & Development Commands

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Build a module (after building ebuild)
ebuild build path/to/module.ebuild.cs

# Generate compile_commands.json for IDE support
ebuild generate compile_commands.json path/to/module.ebuild.cs

# Check for circular dependencies
ebuild check circular-dependency path/to/module.ebuild.cs
```

## File Organization Conventions

- Commands in `ebuild/Commands/` inherit from base command classes
- Compiler implementations in `ebuild/Compilers/` with matching factories
- Platform implementations in `ebuild/Platforms/`
- Module examples in `examples/` directory
- API interfaces in `ebuild.api/` with concrete implementations in `ebuild/`

## Extension Points

The system is designed for extensibility but currently doesn't load external assemblies. Key extension interfaces:
- `IToolchain` for new build toolchains
- `CompilerBase` for new compilers  
- `PlatformBase` for new target platforms
- `IModuleFile` for alternative module definition formats