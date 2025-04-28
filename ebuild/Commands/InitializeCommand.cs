using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.CompilerServices;
using ebuild.api;

namespace ebuild.Commands;


class InitializeCommand
{
    private readonly Command _command = new("init", "initialize a module in the current directory.");

    private static readonly Option<string> _name = new(new[] { "--name", "-n" }, description: "The name of the module to create. This will be used as the module name. If not specified, the directory name will be used.");

    private static readonly Option<string> _fileName = new(new[] { "--file", "-f" }, () => "index.ebuild.cs",
    "The file name to create. This will be used as the module name and the directory name.");

    private static readonly Option<ModuleType> _moduleType = new(new[] { "--type", "-t" }, () => ModuleType.StaticLibrary,
    "The type of the module to create. This will be used as the module type. If not specified, the default is Library.");

    public static readonly Option<bool> _createExampleSource = new(new[] { "--example-source", "-e" }, () => true,
    "Create an example source file and setup.");
    public InitializeCommand()
    {
        _command.SetHandler(Execute);
        {
            // Set the default name to the current directory name
            var currentDirectory = Environment.CurrentDirectory;
            var directoryName = Path.GetFileName(currentDirectory);
            if (!string.IsNullOrEmpty(directoryName))
            {
                _name.SetDefaultValue(directoryName);
            }
        }
    }

    private static async Task Execute(InvocationContext context)
    {
        var name = context.ParseResult.GetValueForOption(_name) ?? "module_name";
        name = name.Replace(" ", "_").Replace("-", "_").Replace(".", "_").Replace("\\", "_").Replace("/", "_").Replace(":", "_").Replace(";", "_").Replace("?", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace("*", "_").Replace("\"", "_").Replace("'", "_").Replace(",", "_").Replace("&", "_").Replace("%", "_").Replace("$", "_").Replace("#", "_").Replace("@", "_").Trim('_');
        string fileName = context.ParseResult.GetValueForOption(_fileName) ?? "index.ebuild.cs";
        Directory.CreateDirectory("source");

        var executableFileContent = @"#include <iostream>
        int main(int argc, char** argv) 
        {{
            std::cout << ""Hello, {name}!\"" << std::endl;
            return 0;
        }}";
        var staticLibFileContent = @"#include <iostream>
        void hello() 
        {{
            std::cout << ""Hello, {name}!\"" << std::endl;
        }}";
        // TODO: Move the NAME_API definition to a separate generation system.
        var sharedLibFileContent = $@"#include <iostream>
        #ifdef {name.ToUpperInvariant()}_BUILDING
        #ifdef _WIN32
        #define {name.ToUpperInvariant()}_API __declspec(dllexport)
        #else
        #define {name.ToUpperInvariant()}_API __attribute__((visibility(""default"")))
        #else
        #ifdef _WIN32
        #define {name.ToUpperInvariant()}_API __declspec(dllimport)
        #else
        #define {name.ToUpperInvariant()}_API __attribute__((visibility(""default"")))
        #endif
        #endif
        {name.ToUpperInvariant()}_API void hello() 
        {{
            std::cout << ""Hello, {name}!\"" << std::endl;
        }}";
        var fileContent = context.ParseResult.GetValueForOption(_moduleType) switch
        {
            ModuleType.Executable => executableFileContent,
            ModuleType.ExecutableWin32 => executableFileContent,
            ModuleType.StaticLibrary => staticLibFileContent,
            ModuleType.SharedLibrary => sharedLibFileContent,
            _ => throw new ArgumentOutOfRangeException()
        };
        bool createExampleSource = context.ParseResult.GetValueForOption(_createExampleSource);
        if (createExampleSource)
        {
            await File.WriteAllTextAsync($"source/main.cpp", fileContent);
        }


        var ebuildFileContent = $@"using ebuild.api;
        class {name}Module : ModuleBase
        {{
            public {name}Module(ModuleContext context) : base(context) {{
                // Set the module name and output directory
                Name = ""{name}"";
                // Set the module type
                Type = ModuleType.{context.ParseResult.GetValueForOption(_moduleType).ToString("G")};
                // Set the cpp standard
                CppStandard = CppStandards.Cpp20;
                {(createExampleSource ? "SourceFiles.AddRange(this.GetAllSourceFiles(\"source\", \"cpp\", \"c\", \"hpp\"));\nIncludes.Public.Add(\"source\")" : "")}
            }}
        }}";
        await File.WriteAllTextAsync(fileName, ebuildFileContent);
        var reference = new ModuleReference(fileName);
        ModuleInstancingParams instancingParams = new ModuleInstancingParams(reference);
        await ModuleFile.Get(reference).CreateModuleInstance(instancingParams);
    }
    public static implicit operator Command(InitializeCommand i) => i._command;

}
