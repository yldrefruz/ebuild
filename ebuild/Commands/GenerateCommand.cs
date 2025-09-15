using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;

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

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var moduleFile = (ModuleFile)ModuleInstancingParams.SelfModuleReference;
            var module = await moduleFile.CreateModuleInstance(ModuleInstancingParams) ?? throw new CommandException("Failed to create module instance.");
            var compiler = await ModuleInstancingParams.Toolchain.CreateCompiler(module, ModuleInstancingParams);
            // TODO: Move this to the Build Graph system.
            // if (!await compiler.Generate("CompileCommandsJSON", OutFile))
            // {
            //     throw new CommandException("Failed to generate compile_commands.json.");
            // }
        }
    }
}