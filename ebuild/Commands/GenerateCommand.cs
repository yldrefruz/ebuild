using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using ebuild.api;
using ebuild.Modules;
using ebuild.Modules.BuildGraph;

namespace ebuild.Commands
{
    [Command("generate", Description = "generate data for the module.")]
    public class GenerateCommand : ModuleCreatingCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
        }
    }


    [Command("generate compile_commands.json", Description = "generate compile_commands.json for the module.")]
    public class GenerateCompileCommandsJsonCommand : GenerateCommand
    {
        [CommandOption("outfile", 'o', Description = "the file to output for, directories will be created.")]
        public string OutFile { get; init; } = "compile_commands.json";
        [CommandOption("dependencies", 'd', Description = "also generate for dependencies.")]
        public bool ShouldDoForDependencies { get; init; } = false;

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var createdModule = await moduleFile.CreateModuleInstance(ModuleInstancingParams) ?? throw new CommandException("Failed to create module instance.");
            var graph = (await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams))!;
            var worker = graph.CreateWorker<GenerateCompileCommandsJsonWorker>();
            worker.GlobalMetadata["compile_commands_module_registry"] = new Dictionary<ModuleBase, List<JsonObject>>();
            if (!ShouldDoForDependencies)
                worker.GlobalMetadata["target_module"] = createdModule;

            await (worker as IWorker).ExecuteAsync(console.RegisterCancellationHandler());
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
                    console.Output.WriteLine($"Generated {outputPath}");
                }
            }
        }

        private static JsonSerializerOptions writeOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    }

    [Command("generate buildgraph", Description = "generate a representation of the build graph for the module. Write to stdout.")]
    public class GenerateBuildGraphString : GenerateCommand
    {
        public enum Format
        {
            String,
            Html
        }
        [CommandOption("format", 'f', Description = "the output format, string or json.")]
        public Format OutputFormat { get; init; } = Format.String;
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var createdModule = await moduleFile.CreateModuleInstance(ModuleInstancingParams) ?? throw new CommandException("Failed to create module instance.");
            var graph = (await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams))!;
            switch (OutputFormat)
            {
                case Format.String:
                    console.Output.WriteLine(graph.CreateTreeString());
                    break;
                case Format.Html:
                    console.Output.WriteLine(graph.CreateTreeHtml());
                    break;
                default:
                    throw new CommandException($"Unsupported format: {OutputFormat}");
            }
        }
    }


    [Command("generate module", Description = "generate a new module file or update the c# solution to include references to dependencies of the module.")]
    public class GenerateModuleCommand : BaseCommand
    {
        override public async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var generator = FindGenerator(Template) ?? throw new CommandException($"Module file generator '{Template}' not found.");
            if (!Update)
                generator.Generate(ModuleFile, Force, TemplateOptions);
            if (Update)
            {
                generator.UpdateSolution(ModuleFile);
            }
        }

        [CommandParameter(0, Description = "the module name to create. If not specified, the created file will be index.ebuild.cs", IsRequired = false)]
        public string ModuleFile { get; init; } = "index.ebuild.cs";

        [CommandOption("force", 'f', Description = "overwrite existing module file if it exists")]
        public bool Force { get; init; } = false;

        [CommandOption("update", 'u', Description = "update the c# solution to include dependencies of the module")]
        public bool Update { get; init; } = false;

        [CommandOption("template", 't', Description = "the module template to use when creating a new module file")]
        public string Template { get; init; } = "default";
        [CommandOption("template-options", 'O', Description = "the options to pass into the module template, in key=value;key2=value2 format", Converter = typeof(OptionsArrayConverter))]
        public OptionsArray TemplateOptions { get; init; } = new();
        IModuleFileGenerator FindGenerator(string Name)
        {
            return ModuleFileGeneratorRegistry.Instance.GetAll().First(g => g.Name == Name);
        }
    }
}