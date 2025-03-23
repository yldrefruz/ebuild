using System.Text;
using ebuild.api;

namespace ebuild;

public class DependencyTree
{
    private Entry? _root;

    private class Entry(Entry? parent, ModuleFile module)
    {
        public Entry? Parent = parent;
        public readonly ModuleFile Module = module;
        public readonly List<Entry> Children = new();

        public int GetDepth()
        {
            int i = 0;
            Entry? localParent = this;
            while (localParent != null)
            {
                localParent = Parent;
                ++i;
            }

            return i;
        }

        public bool IsCircular()
        {
            return IsCircular(this);
        }

        private bool IsCircular(Entry lookFor)
        {
            if (lookFor.GetHashCode() == GetHashCode() && lookFor != this)
                return true;
            return Parent != null && Parent.IsCircular(lookFor);
        }

        public void Append(Entry entry)
        {
            if (entry.IsCircular())
                return;
            Children.Add(entry);
            entry.Parent = this;
        }

        public string GetCircularDependencyGraphString()
        {
            return GetCircularDependencyGraphString(new HashSet<Entry>());
        }

        private string GetCircularDependencyGraphString(ISet<Entry> visited, int depth = 0)
        {
            if (visited.Contains(this))
            {
                return string.Empty;
            }

            visited.Add(this);
            StringBuilder stringBuilder = new();
            var isCircular = IsCircular();
            stringBuilder.Append((isCircular ? Module.Name + " (circular dependency)" : Module.Name) +
                                 '\n');
            if (isCircular)
                return stringBuilder.ToString();
            foreach (var child in Children)
            {
                stringBuilder.Append(new string(' ', (depth + 1) * 2) + "|-");
                stringBuilder.Append(child.GetCircularDependencyGraphString(visited, depth + 1) + '\n');
            }

            return stringBuilder.ToString();
        }

        public override string ToString()
        {
            return GetCircularDependencyGraphString();
        }

        public override int GetHashCode()
        {
            return Module.GetHashCode();
        }
    }

    public bool IsEmpty()
    {
        return _root == null;
    }

    public async Task CreateFromModuleFile(ModuleFile module, string configuration, PlatformBase platform,
        string compilerName)
    {
        _root = new Entry(null, module);
        await CreateFromModuleFile(_root, configuration, platform, compilerName);
    }

    private static async Task CreateFromModuleFile(Entry entry, string configuration, PlatformBase platform,
        string compilerName)
    {
        foreach (var dependency in await entry.Module.GetAllDependencies(configuration, platform, compilerName))
        {
            var child = new Entry(entry, dependency);
            //TODO: Remove this log.
            Console.WriteLine($"Adding dependency {dependency.Name} to {entry.Module.Name}");
            entry.Append(child);
            if (child.IsCircular())
            {
                Console.WriteLine($"Circular dependency detected: {child.Module.Name}");
                continue;
            }

            await CreateFromModuleFile(child, configuration, platform, compilerName);
        }
    }

    public bool HasCircularDependency()
    {
        if (_root == null)
        {
            return false;
        }

        return _root.Children.Any(c => c.IsCircular());
    }

    public string GetCircularDependencyGraphString()
    {
        if (_root == null)
        {
            return string.Empty;
        }

        return _root.GetCircularDependencyGraphString();
    }

    public override string ToString()
    {
        if (_root == null)
        {
            return string.Empty;
        }

        return _root.ToString();
    }
}