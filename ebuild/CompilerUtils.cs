using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild;

public static class CompilerUtils
{
    private static readonly ILogger Logger =
        LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = true;
            }))
            .CreateLogger("Compiler Utils");

    public static string GetObjectOutputFolder(ModuleBase module)
    {
        if (module == null)
            throw new NullReferenceException("CurrentModule is null.");
        
        if (module.UseVariants)
            return Path.Join(module.Context.ModuleDirectory!.FullName, ".ebuild", 
                ((ModuleFile)module.Context.SelfReference).Name, "build", 
                module.GetVariantId().ToString(), "obj") + Path.DirectorySeparatorChar;
        
        return Path.Join(module.Context.ModuleDirectory!.FullName, ".ebuild", 
            ((ModuleFile)module.Context.SelfReference).Name, "build", "obj") + Path.DirectorySeparatorChar;
    }

    public static void ClearObjectAndPdbFiles(ModuleBase module, bool shouldLog = true)
    {
        var objectOutputFolder = GetObjectOutputFolder(module);
        var objectPdbFolder = objectOutputFolder; // Same as object output folder for both compilers
        
        List<string> files =
        [
            .. Directory.GetFiles(objectOutputFolder, "*.obj", SearchOption.TopDirectoryOnly),
            .. Directory.GetFiles(objectPdbFolder, "*.pdb", SearchOption.TopDirectoryOnly),
        ];
        
        foreach (var file in files)
        {
            if (shouldLog)
                Logger.LogDebug("Compilation file {file} is being removed", file);
            try
            {
                File.Delete(Path.GetFullPath(file));
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}