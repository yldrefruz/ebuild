using ebuild.api;

namespace ebuild;

public class DependencyTree
{
    public DependencyTree(ModuleBase module, ModuleContext mainModuleContext)
    {
        AddNode(module);
        
    }

    private void AddNodesRecursive(ModuleBase module, ModuleBase? parent, AccessLimit accessLimit)
    {
        AddNode(module, parent, accessLimit);
        foreach(var dependency in module.Dependencies.Public)
        {
            var mf = new ModuleFile(dependency, module);
            var moduleInstance = mf.CreateModuleInstance()
        }
    }

    private CreateModuleContext(ModuleBase module, ModuleBase? parent, ModuleContext parentContext)
    {
        new ModuleContext()
    }
    
    private class Node
    {
        public Node(ModuleBase element)
        {
            Element = element;
        }

        public HashSet<Node> Parents = new();
        public ModuleBase Element;
        public AccessLimit AccessLimit = AccessLimit.Public;
        public readonly HashSet<Node> Children = new();
    }

    private Node? _root = null;
    private Dictionary<ModuleBase, Node> _moduleNodes = new();

    private Node CreateOrFindNode(ModuleBase element)
    {
        return _moduleNodes.TryGetValue(element, out var node) ? node : new Node(element);
    }

    private void AddNode(ModuleBase inElement, ModuleBase? parent, AccessLimit limit)
    {
        if (_root == null)
        {
            _root = new Node(inElement);
            return;
        }

        if (_root != null && parent != null)
        {
            throw new ArgumentException("parent should be valid as the root already exists.");
        }

        Node parentNode = CreateOrFindNode(parent!);
        Node elementNode = CreateOrFindNode(inElement);
        parentNode.Children.Add(elementNode);
        elementNode.Parents.Add(parentNode);
        parentNode.AccessLimit = limit;
    }

    public List<string> CircularDependencies = new();
}