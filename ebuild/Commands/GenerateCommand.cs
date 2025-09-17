using System.Text.Json;
using System.Text.Json.Nodes;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using ebuild.api;
using ebuild.Modules.BuildGraph;

namespace ebuild.Commands
{
    [Command("generate", Description = "generate data for the module.")]
    public class GenerateCommand : ModuleCreatingCommand
    {
        public override ValueTask ExecuteAsync(IConsole console)
        {
            throw new CommandException("Please specify what to generate.");
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
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var createdModule = await moduleFile.CreateModuleInstance(ModuleInstancingParams) ?? throw new CommandException("Failed to create module instance.");
            var graph = (await moduleFile.BuildOrGetBuildGraph(ModuleInstancingParams))!;
            var worker = graph.CreateWorker<GenerateCompileCommandsJsonWorker>();
            worker.GlobalMetadata["compile_commands_module_registry"] = new Dictionary<ModuleBase, List<JsonObject>>();
            if(!ShouldDoForDependencies)
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
}