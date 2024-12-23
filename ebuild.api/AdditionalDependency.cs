namespace ebuild;

public class AdditionalDependency
{
    public enum DependencyType
    {
        File,
        Directory
    }

    public DependencyType Type { get; }

    public string Path { get; }

    public string? Target { get; }

    public CustomProcessor? Processor { get; }

    public AdditionalDependency(string path)
    {
        Path = path;
        if (Directory.Exists(path))
        {
            Type = DependencyType.Directory;
        }
        else if (File.Exists(path))
        {
            Type = DependencyType.File;
        }
        else
        {
            throw new ArgumentException($"Invalid dependency {path}, couldn't be found or detect the type");
        }
    }

    public AdditionalDependency(string path, CustomProcessor processor) : this(path)
    {
        Processor = processor;
    }

    public AdditionalDependency(string path, string target) : this(path)
    {
        Target = target;
    }

    public AdditionalDependency(string path, string target, CustomProcessor processor) : this(path, processor)
    {
        Target = target;
    }

    public delegate void CustomProcessor(string path, string target);
}