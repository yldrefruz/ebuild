using System.Text.Json.Nodes;
using ebuild.api;
using ebuild.api.Compiler;
using ebuild.Modules.BuildGraph;

namespace ebuild.BuildGraph
{
    class CompileSourceFileNode(CompilerBase compiler, CompilerSettings settings) : Node("CompileSourceFile")
    {
        public CompilerBase Compiler = compiler;
        public CompilerSettings Settings = settings;
        private static readonly object _moduleRegistryLock = new();

        public async override Task ExecuteAsync(IWorker worker, CancellationToken cancellationToken = default)
        {
            if (worker is GenerateCompileCommandsJsonWorker)
            {
                if (Parent is ModuleDeclarationNode parentModuleNode)
                {
                    Dictionary<ModuleBase, List<JsonObject>> compileCommandsModuleRegistry = worker.GlobalMetadata["compile_commands_module_registry"] as Dictionary<ModuleBase, List<JsonObject>> ?? throw new Exception("Global metadata compile_commands_module_registry is not of the correct type.");
                    // Get the registry from the global metadata
                    if (compileCommandsModuleRegistry == null)
                    {
                        throw new Exception("Global metadata compile_commands_module_registry is null");
                    }
                    compileCommandsModuleRegistry.TryGetValue(parentModuleNode.Module, out List<JsonObject>? possibleList);
                    // If the list exists for the module, use it otherwise create and assign it.
                    if (possibleList == null)
                    {
                        lock (_moduleRegistryLock)
                        {
                            possibleList = compileCommandsModuleRegistry[parentModuleNode.Module] = [];
                        }
                    }
                    
                    await Compiler.Generate(Settings, cancellationToken, "compile_commands.json", possibleList);
                }

            }
            else
            {
                await Compiler.Compile(Settings, cancellationToken);
            }
        }

        public override string ToString() => $"CompileSourceFileNode({Settings.SourceFile})";
    }
}
