namespace ebuild.api;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CompilerAttribute : Attribute
{
    private readonly string _name;

    public CompilerAttribute(string name)
    {
        _name = name;
    }


    public string GetName() => _name;
    
}