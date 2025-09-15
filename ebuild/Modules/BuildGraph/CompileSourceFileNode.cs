using ebuild.api.Compiler;
using ebuild.Modules.BuildGraph;

namespace ebuild.BuildGraph
{
    class CompileSourceFileNode(CompilerBase compiler, CompilerSettings settings) : Node("CompileSourceFile")
    {
        public CompilerBase Compiler = compiler;
        public CompilerSettings Settings = settings;

        public async override Task ExecuteAsync(Worker worker, CancellationToken cancellationToken = default)
        {
            await Compiler.Compile(Settings, cancellationToken);
        }

        public override string ToString() => $"CompileSourceFileNode({Settings.SourceFile})";
    }
}
