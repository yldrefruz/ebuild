using ebuild.api;

namespace ebuild.Modules.BuildGraph;


class Graph(ModuleBase module)
{
    public ModuleBase Module { get; init; } = module;
    private Node RootNode = new ModuleDeclarationNode(module);

    public Node GetRootNode() => RootNode;
    public Worker CreateWorker() => new(this);
}