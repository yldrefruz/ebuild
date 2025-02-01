namespace ebuild.api;

public class ModuleReference
{
    private string _file;
    
    ModuleReference(string path)
    {
        _file = path;
    }


    public string GetPureFile() => _file;
}