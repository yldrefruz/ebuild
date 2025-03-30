# EBuild

EBuild is a powerful build system designed to handle complex module-based projects. While inspired by the modularity and flexibility of systems like Unreal Build Tool, EBuild is an independent project with no connection to Unreal Build Tool or Epic Games. It provides a flexible and extensible framework for compiling, linking, and managing dependencies across various platforms and compilers. The system is built with modularity in mind, allowing users to define and manage modules with ease.

## Features

- **Modular Design**: EBuild's core revolves around the concept of modules, which encapsulate source files, dependencies, and build configurations.
- **Compiler Abstraction**: Supports multiple compilers, including MSVC and a NullCompiler for testing purposes.
- **Platform Support**: Extensible platform support with built-in platforms like Win32.
- **Dependency Management**: Automatically resolves and manages module dependencies.
- **Customizable Build Options**: Allows users to specify additional compiler and linker options.
- **Logging**: Integrated logging system for debugging and monitoring build processes.

## Getting Started

### Prerequisites

- .NET 8.0 SDK

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

### Usage

EBuild provides a command-line interface for managing builds. The main entry point is the `ebuild` executable.

#### Commands

- **Help**
The project is still in development process. For more updated usages of commands use
  ```bash
  ebuild --help
  ```

- **Build**: Compile and link the specified module.
  ```bash
  ebuild build <module-file> [options]
  ```
  Options:
  - `--noCompile`: Disable compilation.
  - `--clean`: Clean compilation.
  - `--process-count <number>`: Specify the number of processes for parallel builds.
  - `--configuration <configuration>`: Configuration to use for compiling. Default is loaded from config.
  - `--compiler <compiler name>`: Specify the compiler to use. Default will load from the config. If config doesn't have section, will default to platform's default.
  - `--additional-compiler-option <option1> <option2> ...`: Add option to pass into compiler executable.
  - `--additional-linker-option <option1> <option2> ...`: Add option to pass into linker executable.

- **Generate**: Generate metadata for the module. Generating compile_commands.json can be achieved with this command
  ```bash
  ebuild generate compile_commands.json <module file>
  ```

## Module System

Modules are the core building blocks of EBuild. Each module represents a unit of code with its own source files, dependencies, and build configurations.

### Defining a Module

A module is defined in a `.ebuild.cs` file. Below is an example of a simple module definition:

```csharp
using ebuild.api;

public class MyModule : ModuleBase
{
    public MyModule(ModuleContext context) : base(context)
    {
        Name = "MyModule";
        Type = ModuleType.StaticLibrary;
        Includes.Public.Add("include");
        SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, "src", "cpp", "h"));
        Libraries.Public.Add("SomeLibrary.lib");
    }
}
```

### Module Features

- **Includes**: Specify include directories for the module.
- **Source Files**: Add source files to be compiled.
- **Dependencies**: Define other modules or libraries this module depends on.
- **Build Configuration**: Customize build options such as compiler flags and target architecture.

### Advanced Module Example

```csharp
using ebuild.api;

public class AdvancedModule : ModuleBase
{
    public AdvancedModule(ModuleContext context) : base(context)
    {
        Name = "AdvancedModule";
        Type = ModuleType.Executable;
        Includes.Public.AddRange(new[] { "include", "third_party/include" });
        SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, "src", "cpp", "h"));
        Libraries.Public.AddRange(new[] { "SomeLibrary.lib", "AnotherLibrary.lib" });
        Definitions.Public.Add("DEBUG=1");
        Options.Add("/O2");
    }
}
```

## Extending EBuild

The api for extending is complete. But the ebuild itself currently does **not** load the extension assemblies for now.

After thinking about the way to load the assemblies in to the program, the extension system will be completed.

### Adding a New Compiler

To add a new compiler, create a class that inherits from `CompilerBase` and implement the required methods:

```csharp
using ebuild.api;

[Compiler("MyCompiler")]
public class MyCompiler : CompilerBase
{
    public override Task<bool> Setup() { /* Setup logic */ }
    public override Task<bool> Compile() { /* Compilation logic */ }
    public override string GetExecutablePath() { /* Path to compiler executable; better if programatically created*/ }
}
```

### Adding a New Platform

To add a new platform, create a class that inherits from `PlatformBase` and implement the required methods:

```csharp
using ebuild.api;

[Platform("MyPlatform")]
public class MyPlatform : PlatformBase
{
    public override string GetDefaultCompilerName() => "MyCompiler";
}
```

## Contributing

Contributions are welcome! Please submit a pull request or open an issue to discuss your ideas.

## License

This project is licensed under the MIT License. See  LICENSE file.