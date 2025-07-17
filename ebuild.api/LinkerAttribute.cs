namespace ebuild.api;

[AttributeUsage(AttributeTargets.Class)]
public class LinkerAttribute : Attribute
{
    private readonly string _name;

    public LinkerAttribute(string name)
    {
        _name = name;
    }

    public string GetName()
    {
        return _name;
    }
}