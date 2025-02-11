namespace ebuild.api;

public static class ModuleUtilities
{
    public static string[] GetAllSourceFiles(this ModuleBase module, string root, params string[] extensions)
    {
        List<string> files = new();
        string findAt = Path.Join(module.Context.ModuleDirectory!.FullName, root);
        foreach (var extension in extensions)
        {
            files.AddRange(Directory.GetFiles(findAt, "*." + extension, SearchOption.AllDirectories));
        }

        return files.ToArray();
    }
}