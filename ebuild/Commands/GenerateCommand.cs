using System.Text.Json;
using System.Text.Json.Nodes;
using ebuild.api;
using ebuild.cli;
using ebuild.Modules;
using ebuild.Modules.BuildGraph;

namespace ebuild.Commands
{
    [Command("generate compile_commands.json", Description = "generate compile_commands.json for the module.")]
    public class GenerateCompileCommandsJsonCommand : ModuleCreatingCommand
    {
        [Option("outfile", ShortName = "o", Description = "the file to output for, directories will be created.")]
        public string OutFile = "compile_commands.json";
        [Option("dependencies", ShortName = "d", Description = "also generate for dependencies.")]
        public bool ShouldDoForDependencies = false;

        public override async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var createdModule = await moduleFile.CreateModuleInstance(ModuleInstancingParams) ?? throw new Exception("Failed to create module instance.");
            var graph = (await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams))!;
            var worker = graph.CreateWorker<GenerateCompileCommandsJsonWorker>();
            worker.GlobalMetadata["compile_commands_module_registry"] = new Dictionary<ModuleBase, List<JsonObject>>();
            if (!ShouldDoForDependencies)
                worker.GlobalMetadata["target_module"] = createdModule;

            await (worker as IWorker).ExecuteAsync(cancellationToken);
            (worker as IWorker).GlobalMetadata.TryGetValue("compile_commands_module_registry", out object? value);

            if (value is Dictionary<ModuleBase, List<JsonObject>> compileCommandsModuleRegistry)
            {
                foreach (var kvp in compileCommandsModuleRegistry)
                {
                    var module = kvp.Key;
                    var list = kvp.Value;
                    var outputPath = Path.GetFullPath(OutFile, module.Context.ModuleDirectory.FullName);
                    Directory.CreateDirectory(module.OutputDirectory);
                    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(list, writeOptions));
                    Console.WriteLine($"Generated {outputPath}");
                }
            }
            return 0;
        }

        private static JsonSerializerOptions writeOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    }

    [Command("generate buildgraph", Description = "generate a representation of the build graph for the module. Write to stdout.")]
    public class GenerateBuildGraphString : ModuleCreatingCommand
    {
        public enum Format
        {
            String,
            Html
        }
        [Option("format", ShortName = "f", Description = "the output format, string or json.")]
        public Format OutputFormat = Format.String;
        public override async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var createdModule = await moduleFile.CreateModuleInstance(ModuleInstancingParams) ?? throw new Exception("Failed to create module instance.");
            var graph = (await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams))!;
            switch (OutputFormat)
            {
                case Format.String:
                    Console.WriteLine(graph.CreateTreeString());
                    break;
                case Format.Html:
                    Console.WriteLine(graph.CreateTreeHtml());
                    break;
                default:
                    throw new Exception($"Unsupported format: {OutputFormat}");
            }
            return 0;
        }
    }


    [Command("generate module", Description = "generate a new module file or update the c# solution to include references to dependencies of the module.")]
    public class GenerateModuleCommand : BaseCommand
    {
        override public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var generator = FindGenerator(Template) ?? throw new Exception($"Module file generator '{Template}' not found.");
            if (!Update)
                generator.Generate(ModuleFile, Force, TemplateOptions);
            if (Update)
            {
                generator.UpdateSolution(ModuleFile);
            }
            return 0;
        }

        [Argument(0, Description = "the module name to create. If not specified, the created file will be index.ebuild.cs", IsRequired = false)]
        public string ModuleFile = "index.ebuild.cs";
        [Option("force", ShortName = "f", Description = "overwrite existing module file if it exists")]
        public bool Force = false;
        [Option("update", ShortName = "u", Description = "update the c# solution to include dependencies of the module")]
        public bool Update = false;
        [Option("template", ShortName = "t", Description = "the module template to use when creating a new module file")]
        public string Template = "default";
        [Option("template-options", ShortName = "O", Description = "the options to pass into the module template, use multiple to pass in multiple options -OKey=Value or -OKey Value")]
        public Dictionary<string, string> TemplateOptions = new();
        static IModuleFileGenerator FindGenerator(string Name)
        {
            return ModuleFileGeneratorRegistry.Instance.GetAll().First(g => g.Name == Name);
        }
    }
}