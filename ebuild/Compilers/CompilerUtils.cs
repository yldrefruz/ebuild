using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild
{
    public static class CompilerUtils
    {
        private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("Compiler Utils");

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

        public static List<string> FindBuildArtifacts(ModuleBase module, bool includeObjectFiles = true, bool includePdbFiles = true, bool includeStaticLibraries = false, bool includeDynamicLibraries = false, bool includeExecutables = false)
        {
            var objectOutputFolder = GetObjectOutputFolder(module);
            if(objectOutputFolder == null || !Directory.Exists(objectOutputFolder))
            {
                Logger.LogInformation("Object output folder {folder} does not exist", objectOutputFolder);
                return new List<string>();
            }
            var files = new List<string>();
        
            if (includeObjectFiles)
                files.AddRange(Directory.GetFiles(objectOutputFolder, "*.obj", SearchOption.TopDirectoryOnly));
        
            if (includePdbFiles)
                files.AddRange(Directory.GetFiles(objectOutputFolder, "*.pdb", SearchOption.TopDirectoryOnly));
        
            if (includeStaticLibraries)
                files.AddRange(Directory.GetFiles(objectOutputFolder, "*.lib", SearchOption.TopDirectoryOnly));
        
            if (includeDynamicLibraries)
            {
                files.AddRange(Directory.GetFiles(objectOutputFolder, "*.dll", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.GetFiles(objectOutputFolder, "*.so", SearchOption.TopDirectoryOnly));
            }
        
            if (includeExecutables)
            {
                files.AddRange(Directory.GetFiles(objectOutputFolder, "*.exe", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.GetFiles(objectOutputFolder, "", SearchOption.TopDirectoryOnly)
                    .Where(f => !Path.HasExtension(f) && File.Exists(f)));
            }
        
            return files;
        }

        public static void ClearObjectAndPdbFiles(ModuleBase module, bool shouldLog = true)
        {
            var files = FindBuildArtifacts(module, includeObjectFiles: true, includePdbFiles: true);
        
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
}