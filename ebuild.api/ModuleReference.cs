namespace ebuild.api;

public class ModuleReference
{
    private readonly string _file; // Absolute path to file

    ModuleReference(string path)
    {
        _file = path;
    }

    public static implicit operator ModuleReference(string file) => new ModuleReference(file);

    public string GetPureFile() => _file;
}