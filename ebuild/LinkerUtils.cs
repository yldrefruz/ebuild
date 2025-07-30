using ebuild.api;

namespace ebuild;

public static class LinkerUtils
{
    public static string GetObjectOutputFolder(ModuleBase module)
    {
        if (module == null)
            throw new NullReferenceException("CurrentModule is null.");
        
        // This should match the object output folder used by the compiler
        if (module.UseVariants)
            return Path.Join(module.Context.ModuleDirectory!.FullName, ".ebuild", 
                ((ModuleFile)module.Context.SelfReference).Name, "build", 
                module.GetVariantId().ToString(), "obj") + Path.DirectorySeparatorChar;
        
        return Path.Join(module.Context.ModuleDirectory!.FullName, ".ebuild", 
            ((ModuleFile)module.Context.SelfReference).Name, "build", "obj") + Path.DirectorySeparatorChar;
    }

    public static string GetBinaryOutputFolder(ModuleBase module)
    {
        if (module == null)
            throw new NullReferenceException("CurrentModule is null.");
        
        return module.GetBinaryOutputDirectory();
    }

    public static string GetModuleFilePath(string path, ModuleBase module)
    {
        var fp = Path.GetFullPath(path, module.Context.ModuleDirectory!.FullName);
        return fp;
    }
}