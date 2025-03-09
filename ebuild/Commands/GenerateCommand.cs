using System.CommandLine;
using System.CommandLine.Invocation;
using ebuild.api;
using ebuild.Compilers;

namespace ebuild.Commands;

public class GenerateCommand
{
    private readonly Command _command = new("generate", "generate meta data for the module.");

    private class CompileCommandsJsonCommand
    {
        private readonly Command _command = new("compile_commands.json");

        private readonly Option<string> _file = new(new[] { "--outfile", "-o" }, () => "compile_commands.json",
            "the file to output for, directories will be created.");

        public CompileCommandsJsonCommand()
        {
            _command.AddOption(_file);
            _command.AddCompilerCreationParams();
            _command.SetHandler(Execute);
        }

        private async Task<int> Execute(InvocationContext context)
        {
            var cp = await CompilerRegistry.CreateInstanceFor(
                CompilerRegistry.CompilerInstancingParams.FromOptionsAndArguments(context));
            if (cp == null) return 2;
            return await cp.Generate("CompileCommandsJSON", context.ParseResult.GetValueForOption(_file)) ? 0 : 1;
        }


        public static implicit operator Command(CompileCommandsJsonCommand c) => c._command;
    }

    public GenerateCommand()
    {
        _command.AddCommand(new CompileCommandsJsonCommand());
    }

    public static implicit operator Command(GenerateCommand g) => g._command;
}