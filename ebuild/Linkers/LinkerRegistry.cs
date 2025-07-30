using System.Reflection;
using ebuild.api;

namespace ebuild.Linkers;

public class LinkerRegistry
{
    private static LinkerRegistry? _instance;
    private readonly Dictionary<string, Type> _linkers = new();

    private LinkerRegistry()
    {
    }

    public static LinkerRegistry GetInstance()
    {
        return _instance ??= new LinkerRegistry();
    }

    public void RegisterAllFromAssembly(Assembly assembly)
    {
        var linkerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(LinkerBase)))
            .ToList();

        foreach (var linkerType in linkerTypes)
        {
            var linkerAttribute = linkerType.GetCustomAttribute<LinkerAttribute>();
            if (linkerAttribute != null)
            {
                Register(linkerAttribute.GetName(), linkerType);
            }
        }
    }

    public void Register(string name, Type linkerType)
    {
        if (!linkerType.IsSubclassOf(typeof(LinkerBase)))
        {
            throw new ArgumentException($"Type {linkerType.Name} must inherit from LinkerBase");
        }

        if (_linkers.ContainsKey(name))
        {
            throw new ArgumentException($"Linker with name '{name}' is already registered");
        }

        _linkers[name] = linkerType;
    }

    public T Get<T>() where T : LinkerBase
    {
        var linkerType = typeof(T);
        var linkerAttribute = linkerType.GetCustomAttribute<LinkerAttribute>();
        if (linkerAttribute == null)
        {
            throw new InvalidOperationException($"Linker type {linkerType.Name} does not have LinkerAttribute");
        }

        var name = linkerAttribute.GetName();
        if (!_linkers.ContainsKey(name))
        {
            throw new KeyNotFoundException($"Linker '{name}' not found");
        }

        return (T)Activator.CreateInstance(_linkers[name])!;
    }

    public LinkerBase Get(string name)
    {
        if (!_linkers.ContainsKey(name))
        {
            throw new KeyNotFoundException($"Linker '{name}' not found");
        }

        return (LinkerBase)Activator.CreateInstance(_linkers[name])!;
    }
}