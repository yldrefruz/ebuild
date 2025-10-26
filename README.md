# EBuild

EBuild is a powerful, modular C++ build system with a .NET-based CLI designed to handle complex module-based projects. While inspired by the modularity and flexibility of systems like Unreal Build Tool, EBuild is an independent project with no connection to Unreal Build Tool or Epic Games. It provides a flexible and extensible framework for compiling, linking, and managing dependencies across various platforms and compilers.

## Features

- **Modular Design**: EBuild's core revolves around the concept of modules defined in `.ebuild.cs` files, which encapsulate source files, dependencies, and build configurations.
- **Multi-Compiler Support**: 
  - **MSVC Toolchain**: Full support for Microsoft Visual C++ compiler (cl.exe), linker (link.exe), librarian (lib.exe), and resource compiler (rc.exe)
  - **GCC Toolchain**: Complete GCC/G++ support with AR for static libraries
  - Extensible compiler abstraction allowing custom compiler implementations
- **Cross-Platform Support**: 
  - Windows (Win32) platform with MSVC as default toolchain
  - Unix platform with GCC as default toolchain
  - Extensible platform system for custom targets
- **Advanced Dependency Management**: 
  - Automatic dependency resolution with circular dependency detection
  - Access control system (Public/Private) for transitive dependency propagation
  - Module variants based on build options
- **Flexible Build Graph**: Creates and resolves complex dependency graphs with caching for efficient builds
- **Module Options**: Define configurable module parameters that can affect binary output and create build variants
- **Parallel Builds**: Configurable worker count for concurrent compilation
- **IDE Integration**: Generate `compile_commands.json` for IDE language server support
- **Logging**: Integrated logging system with verbose mode for debugging build processes

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- A C++ compiler toolchain:
  - **Windows**: Visual Studio 2019 or later (for MSVC toolchain), or MinGW/Cygwin (for GCC toolchain)
  - **Linux/Unix**: GCC/G++ toolchain

### Installation

Clone the repository and build the solution using the .NET CLI or your preferred IDE:

```bash
git clone https://github.com/yldrefruz/ebuild.git
cd ebuild
```

Build the project:

```bash
dotnet build
```

The compiled `ebuild` executable will be available in the build output directory (typically `ebuild/bin/Debug/net8.0/` or `ebuild/bin/Release/net8.0/`).

### Usage

EBuild provides a command-line interface for managing builds. The main entry point is the `ebuild` executable.

#### Commands

For the most up-to-date command usage, use:
```bash
ebuild --help
```

##### Build Command

Compile and link the specified module:
```bash
ebuild build <module-file> [options]
```

**Options:**
- `-c, --configuration <configuration>`: Build configuration (default: `debug`)
- `--toolchain <name>`: Specify the toolchain to use (e.g., `msvc`, `gcc`)
- `-t, --target-architecture <arch>`: Target architecture (e.g., `X64`, `X86`, `Arm64`)
- `-p, --build-worker-count <number>`: Number of parallel build workers (default: 1)
- `-n, --dry-run`: Perform a trial run without actual compilation
- `--clean`: Perform a clean build
- `-C, --additional-compiler-option <option>`: Additional compiler options (can be specified multiple times)
- `-L, --additional-linker-option <option>`: Additional linker options (can be specified multiple times)
- `-P, --additional-dependency-path <path>`: Additional paths to search for dependency modules
- `-D, --option <key=value>`: Module options to pass (format: `key=value`)
- `-v, --verbose`: Enable verbose logging

**Example:**
```bash
ebuild build examples/zlib/zlib.ebuild.cs -c release -p 4 --verbose
```

##### Generate Commands

**Generate compile_commands.json:**
```bash
ebuild generate compile_commands.json <module-file> [options]
```

Options:
- `-o, --outfile <path>`: Output file path (default: `compile_commands.json`)
- `-d, --dependencies`: Also generate for dependencies

This generates a compilation database for IDE integration and language servers (clangd, ccls, etc.).

**Generate Build Graph:**
```bash
ebuild generate buildgraph <module-file> [options]
```

Options:
- `--format <format>`: Output format - `String` or `Html` (default: `String`)

Outputs a visual representation of the build dependency graph.

##### Check Commands

**Check for Circular Dependencies:**
```bash
ebuild check circular-dependencies <module-file>
```

Detects and reports circular dependency chains in the module graph.

**Print Dependencies:**
```bash
ebuild check print-dependencies <module-file>
```

Displays the complete dependency tree for the module.

##### Property Commands

**Get Properties:**
```bash
ebuild property get <property-name>
```

Available properties:
- `ebuild.api.dll`: Returns the path to the ebuild.api.dll assembly

## Module System

Modules are the core building blocks of EBuild. Each module represents a unit of code with its own source files, dependencies, and build configurations. Modules are defined in `.ebuild.cs` files and inherit from `ModuleBase`.

### Defining a Module

A basic module definition:

```csharp
using ebuild.api;

public class MyModule : ModuleBase
{
    public MyModule(ModuleContext context) : base(context)
    {
        Name = "MyModule";
        Type = ModuleType.StaticLibrary; // or SharedLibrary, Executable, ExecutableWin32
        
        // Public includes are propagated to dependent modules
        Includes.Public.Add("include");
        
        // Private includes are only used by this module
        Includes.Private.Add("src");
        
        // Add source files
        SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, "src", "cpp", "h"));
        
        // Public libraries are passed to dependent modules
        Libraries.Public.Add("SomeLibrary.lib");
        
        // Public dependencies become transitive dependencies
        Dependencies.Public.Add(new ModuleReference("PublicDependency"));
        
        // Private dependencies are only linked to this module
        Dependencies.Private.Add(new ModuleReference("PrivateDependency"));
    }
}
```

### Access Control System

EBuild uses an `AccessLimitList<T>` pattern for dependency propagation:

- **Public**: Items are propagated to all dependent modules (transitive)
- **Private**: Items are only used within the current module (non-transitive)

Collections with access control:
- `Includes` - Include directories
- `Definitions` - Preprocessor definitions
- `Dependencies` - Module dependencies
- `Libraries` - Link libraries
- `LibrarySearchPaths` - Library search directories
- `ForceIncludes` - Forced includes

Use `.Joined()` to get combined public and private items.

### Module Options

Define configurable parameters using the `[ModuleOption]` attribute:

```csharp
public class ConfigurableModule : ModuleBase
{
    [ModuleOption(Description = "Enable debug logging", ChangesResultBinary = true)]
    public bool EnableDebug = false;
    
    [ModuleOption(Description = "Optimization level", ChangesResultBinary = true)]
    public int OptimizationLevel = 2;
    
    [ModuleOption(Description = "Custom define", ChangesResultBinary = false)]
    public string CustomDefine = "DEFAULT";
    
    public ConfigurableModule(ModuleContext context) : base(context)
    {
        Name = "ConfigurableModule";
        Type = ModuleType.StaticLibrary;
        
        // Use options in configuration
        if (EnableDebug)
        {
            Definitions.Public.Add("ENABLE_DEBUG=1");
        }
        
        CompilerOptions.Add($"/O{OptimizationLevel}");
    }
}
```

Pass options via command line:
```bash
ebuild build module.ebuild.cs -D EnableDebug=true -D OptimizationLevel=3
```

When `ChangesResultBinary = true`, different option values create separate build variants with unique output paths.

### Module Types

- `ModuleType.StaticLibrary` - Static library (.lib/.a)
- `ModuleType.SharedLibrary` - Shared/dynamic library (.dll/.so)
- `ModuleType.Executable` - Console executable
- `ModuleType.ExecutableWin32` - Windows GUI executable (Win32 subsystem)

### Module Features

**Source Files:**
```csharp
SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, "src", "cpp", "h"));
```

**Definitions:**
```csharp
Definitions.Public.Add("MY_DEFINE=1");
Definitions.Private.Add("INTERNAL_USE");
GlobalDefinitions.Add("GLOBAL_DEFINE"); // Applied to all modules
```

**Compiler and Linker Options:**
```csharp
CompilerOptions.Add("/W4");        // MSVC warning level
CompilerOptions.Add("-Wall");      // GCC warnings
LinkerOptions.Add("/SUBSYSTEM:WINDOWS");
```

**Advanced Compiler Settings:**
```csharp
CStandard = CStandards.C17;               // C standard
CppStandard = CppStandards.Cpp20;         // C++ standard
OptimizationLevel = OptimizationLevel.O2; // Optimization
EnableExceptions = true;
EnableRTTI = true;
EnableFastFloatingPointOperations = true;
```

**Additional Dependencies:**
```csharp
AdditionalDependencies.Public.Add(new AdditionalDependency
{
    SourcePath = "path/to/file",
    DestinationPath = "relative/dest",
    IsDirectory = false
});
```

### Advanced Module Example

```csharp
using ebuild.api;

public class AdvancedModule : ModuleBase
{
    [ModuleOption(Description = "Enable extra features", ChangesResultBinary = true)]
    public bool EnableExtraFeatures = false;
    
    public AdvancedModule(ModuleContext context) : base(context)
    {
        Name = "AdvancedModule";
        Type = ModuleType.Executable;
        
        // Public includes for dependent modules
        Includes.Public.AddRange(new[] { "include", "third_party/include" });
        
        // Private implementation includes
        Includes.Private.Add("src/internal");
        
        // Source files
        SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, "src", "cpp", "h"));
        
        // Public dependencies (transitive)
        Dependencies.Public.Add(new ModuleReference("CoreLibrary"));
        
        // Private dependencies (non-transitive)
        Dependencies.Private.Add(new ModuleReference("ThirdPartyLib"));
        
        // Libraries
        Libraries.Public.AddRange(new[] { "SomeLibrary.lib", "AnotherLibrary.lib" });
        LibrarySearchPaths.Public.Add("third_party/lib");
        
        // Definitions
        Definitions.Public.Add("API_VERSION=2");
        if (EnableExtraFeatures)
        {
            Definitions.Public.Add("EXTRA_FEATURES=1");
        }
        
        // Compiler settings
        CppStandard = CppStandards.Cpp20;
        OptimizationLevel = context.Configuration == "release" 
            ? OptimizationLevel.O2 
            : OptimizationLevel.Od;
        
        // Platform-specific configuration
        if (context.Platform.Name == "windows")
        {
            Libraries.Public.Add("user32.lib");
            CompilerOptions.Add("/W4");
        }
        else if (context.Platform.Name == "unix")
        {
            Libraries.Public.Add("pthread");
            CompilerOptions.Add("-Wall");
        }
    }
}
```

### Real-World Example: Zlib Module

The `examples/zlib/` directory contains a complete example that:
- Downloads and extracts zlib source from GitHub
- Verifies SHA256 checksum
- Configures build with module options
- Builds as a static library

See `examples/zlib/zlib.ebuild.cs` for the full implementation.

## Architecture

### Component Overview

EBuild consists of three main components:

1. **ebuild** - CLI application built with CliFx framework
   - Commands: `build`, `generate`, `check`, `property`
   - Compiler implementations: MSVC (cl.exe, rc.exe), GCC
   - Linker implementations: MSVC (link.exe, lib.exe), GCC, AR
   - Platform implementations: Win32, Unix
   - Toolchain implementations: MSVC, GCC

2. **ebuild.api** - Core framework library
   - Base classes: `ModuleBase`, `CompilerBase`, `LinkerBase`, `PlatformBase`
   - Interfaces: `IToolchain`, `ICompilerFactory`, `ILinkerFactory`, `IModuleFile`
   - Core types: `AccessLimitList`, `ModuleReference`, `ModuleContext`
   - Attributes: `[ModuleOption]`, `[OutputTransformer]`, `[Platform]`, `[Compiler]`

3. **ebuild.Tests** - NUnit test suite
   - Integration tests for real build scenarios
   - Unit tests for core functionality

### Toolchain System

EBuild uses a factory pattern for compiler and linker creation:

```
Platform → Default Toolchain → Factories → Compiler/Linker Instances
```

**Built-in Toolchains:**

- **MSVC Toolchain** (`msvc`)
  - Compiler: `MsvcClCompiler` (cl.exe)
  - Resource Compiler: `MsvcRcCompiler` (rc.exe)
  - Linker: `MsvcLinkLinker` (link.exe) for executables/DLLs
  - Archiver: `MsvcLibLinker` (lib.exe) for static libraries

- **GCC Toolchain** (`gcc`)
  - Compiler: `GccCompiler` (gcc/g++)
  - Linker: `GccLinker` (gcc/g++) for executables/shared libraries
  - Archiver: `ArLinker` (ar) for static libraries

### Platform System

**Built-in Platforms:**

- **Win32 Platform** (`windows`)
  - Default toolchain: MSVC
  - File extensions: `.obj`, `.lib`, `.dll`, `.exe`
  - Resource files: `.rc` → `.res`

- **Unix Platform** (`unix`)
  - Default toolchain: GCC
  - File extensions: `.o`, `.a`, `.so`, (no extension for executables)

### Build Graph

The build system constructs a dependency graph:

1. **Module Loading**: `.ebuild.cs` files are compiled and loaded dynamically
2. **Dependency Resolution**: Recursively resolves module dependencies with caching
3. **Circular Detection**: Detects and reports circular dependency chains
4. **Build Ordering**: Topologically sorts modules for correct build order
5. **Parallel Execution**: Executes build steps with configurable worker count

### Access Control & Dependency Propagation

```
Module A (Public dep) → Module B (depends on A) → Module C (depends on B)
```

- Module B gets Module A's public includes, libraries, and definitions
- Module C gets Module A's public items transitively through Module B
- Module A's private items stay within Module A

## Extending EBuild

The API for extending EBuild is complete. However, the system currently does **not** load external extension assemblies. This feature is planned for future development.

### Adding a New Compiler

Create a class inheriting from `CompilerBase`:

```csharp
using ebuild.api;
using ebuild.api.Compiler;

[Compiler("MyCompiler")]
public class MyCompiler : CompilerBase
{
    public override async Task<bool> Compile(CompilerSettings settings, CancellationToken cancellationToken)
    {
        // Implement compilation logic
        // Access settings.SourceFiles, settings.OutputFile, etc.
        // Return true on success
    }
}
```

Create a corresponding factory:

```csharp
public class MyCompilerFactory : ICompilerFactory
{
    public string Name => "my.compiler";
    public Type CompilerType => typeof(MyCompiler);
    
    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        // Return true if this compiler can handle the module
        return true;
    }
    
    public CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new MyCompiler();
    }
    
    public string GetExecutablePath(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return "/path/to/compiler";
    }
}
```

### Adding a New Linker

Create a class inheriting from `LinkerBase`:

```csharp
using ebuild.api.Linker;

public class MyLinker : LinkerBase
{
    public override async Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken)
    {
        // Implement linking logic
        // Access settings.ObjectFiles, settings.OutputFile, etc.
        // Return true on success
    }
}
```

Create a corresponding factory:

```csharp
public class MyLinkerFactory : ILinkerFactory
{
    public string Name => "my.linker";
    public Type LinkerType => typeof(MyLinker);
    
    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return module.Type != ModuleType.StaticLibrary;
    }
    
    public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new MyLinker();
    }
}
```

### Adding a New Platform

Create a class inheriting from `PlatformBase`:

```csharp
using ebuild.api;

[Platform("MyPlatform")]
public class MyPlatform : PlatformBase
{
    public MyPlatform() : base("myplatform")
    {
    }
    
    public override string? GetDefaultToolchainName() => "gcc";
    
    public override string ExtensionForStaticLibrary => ".a";
    public override string ExtensionForSharedLibrary => ".so";
    public override string ExtensionForExecutable => "";
    
    public override IEnumerable<string> GetPlatformDefinitions(ModuleBase module)
    {
        yield return new Definition("MY_PLATFORM", "1");
    }
    
    public override IEnumerable<string> GetPlatformCompilerFlags(ModuleBase module)
    {
        yield return "-fPIC";
    }
    
    public override IEnumerable<string> GetPlatformLibraries(ModuleBase module)
    {
        yield return "pthread";
        yield return "dl";
    }
}
```

### Adding a New Toolchain

Create a class implementing `IToolchain`:

```csharp
using ebuild.api.Toolchain;

public class MyToolchain : IToolchain
{
    public string Name => "mytoolchain";
    
    public ICompilerFactory? GetCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new MyCompilerFactory();
    }
    
    public ILinkerFactory? GetLinkerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        if (module.Type == ModuleType.StaticLibrary)
            return new MyArchiverFactory();
        return new MyLinkerFactory();
    }
    
    public ICompilerFactory? GetResourceCompilerFactory(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return null; // Optional resource compiler
    }
}
```

## Examples

The `examples/` directory contains working examples:

### Zlib Example

Located in `examples/zlib/`, this demonstrates:
- Automatic source downloading and verification
- SHA256 checksum validation
- Module options for build configuration
- Cross-platform static library building

Build it:
```bash
ebuild build examples/zlib/zlib.ebuild.cs
```

With options:
```bash
ebuild build examples/zlib/zlib.ebuild.cs -D EnableDebug=true -D OptimizeForSize=true
```

### Circular Dependency Example

Located in `examples/circular-dependency/`, demonstrates circular dependency detection.

## Development

### Building EBuild

```bash
# Clone the repository
git clone https://github.com/yldrefruz/ebuild.git
cd ebuild

# Build the solution
dotnet build

# Build in release mode
dotnet build -c Release

# Run tests
dotnet test
```

### Testing

The test suite includes:
- **Integration Tests**: Real build scenarios using example modules
- **Unit Tests**: Core functionality tests

Run specific test categories:
```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Project Structure

```
ebuild/
├── ebuild/                 # CLI application
│   ├── Commands/          # Command implementations
│   ├── Compilers/         # Compiler implementations
│   ├── Linkers/          # Linker implementations
│   ├── Platforms/        # Platform implementations
│   ├── Toolchains/       # Toolchain implementations
│   └── Modules/          # Module loading and build graph
├── ebuild.api/           # Core API framework
│   ├── Compiler/         # Compiler abstractions
│   ├── Linker/           # Linker abstractions
│   └── Toolchain/        # Toolchain interfaces
├── ebuild.Tests/         # Test suite
│   ├── Integration/      # Integration tests
│   └── Unit/            # Unit tests
└── examples/            # Example modules
    ├── zlib/           # Zlib build example
    └── circular-dependency/
```

## Contributing

Contributions are welcome! Here's how you can help:

1. **Report Issues**: Open an issue for bugs or feature requests
2. **Submit Pull Requests**: 
   - Fork the repository
   - Create a feature branch
   - Make your changes
   - Add tests if applicable
   - Submit a PR with a clear description
3. **Improve Documentation**: Help improve this README or add inline documentation
4. **Add Examples**: Create example modules demonstrating EBuild features

### Coding Guidelines

- Follow existing code style and conventions
- Add XML documentation comments for public APIs
- Write tests for new features
- Keep commits focused and atomic
- Update README if adding new features

## License

This project is licensed under the MIT License. See the LICENSE file for details.

## Acknowledgments

While inspired by Unreal Build Tool's modularity, EBuild is an independent project with no affiliation to Epic Games or Unreal Engine.