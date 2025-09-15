using ebuild.api.Linker;
using ebuild.Modules.BuildGraph;

namespace ebuild.BuildGraph
{
    class LinkerNode(LinkerBase linker, LinkerSettings settings) : Node("Linker")
    {
        public LinkerBase Linker = linker;
        public LinkerSettings Settings = settings;

        public override async Task ExecuteAsync(Worker worker, CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(worker, cancellationToken);
            bool result = await Linker.Link(Settings, cancellationToken);
            if (!result)
                throw new Exception($"Linking failed for module {Settings.OutputFile}");
        }

        public override string ToString() => $"- LinkerNode({Settings.OutputFile})";
    }
}
