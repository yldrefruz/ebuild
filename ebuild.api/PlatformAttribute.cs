namespace ebuild.api;
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PlatformAttribute : Attribute
{
    private readonly string _name;

    public PlatformAttribute(string name)
    {
        _name = name;
    }


    public string GetName() => _name;
}