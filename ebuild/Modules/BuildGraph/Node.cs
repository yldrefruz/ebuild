using ebuild.api;

namespace ebuild.Modules.BuildGraph;


class Node(string name)
{
    public string Name = name;
    public AccessLimitList<Node> Children = new();
    public Node? Parent = null;

    public void AddChild(Node child, AccessLimit accessLimit = AccessLimit.Public)
    {
        Children.Add(accessLimit, child);
        child.Parent = this;
    }
    public virtual Task ExecuteAsync(Worker worker, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
