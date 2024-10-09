namespace ebuild;

public class ModuleDependency
{
    private string _path;

    public ModuleDependency(string path)
    {
        _path = path;
    }

    private bool GetValidatedModuleFile(ModuleContext context, out string moduleFileAbsolutePath)
    {
        var oldcwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(context.ModuleDirectory);
        var bSuccess = false;
        var localModuleFilePath = "";
        if (Directory.Exists(_path))
        {
            Directory.SetCurrentDirectory(_path);
            //this is a directory so try to get the same ebuild.cs or default.ebuild.cs
            var di = new DirectoryInfo(_path);
            var dirName = di.Name;
            if (File.Exists(String.Format("{0}.ebuild.cs", dirName)))
            {
                localModuleFilePath = Path.GetFullPath(String.Format("{0}.ebuild.cs", dirName));
                bSuccess = true;
            }
            else if (File.Exists("default.ebuild.cs"))
            {
                localModuleFilePath = Path.GetFullPath("default.ebuild.cs");
                bSuccess = true;
            }
        }
        else if (File.Exists(_path) && _path.ToLowerInvariant().EndsWith(".ebuild.cs"))
        {
            localModuleFilePath = Path.GetFullPath(_path);
            bSuccess = true;
        }

        Directory.SetCurrentDirectory(oldcwd);
        moduleFileAbsolutePath = localModuleFilePath;
        return bSuccess;
    }
}