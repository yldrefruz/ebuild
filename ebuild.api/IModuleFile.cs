namespace ebuild.api
{
    public interface IModuleFile
    {
        public Task<ModuleBase?> CreateModuleInstance(IModuleInstancingParams instancingParams);
        public ModuleBase? GetCompiledModule();
        public bool IsCompiled() => GetCompiledModule() != null;
        public ModuleReference GetSelfReference();
        public string GetFilePath();
        public string GetDirectory();
        public bool HasChanged();
        public void UpdateCachedEditTime();

        public static string TryDirToModuleFile(string path, out string name)
        {
            if (File.Exists(path))
            {
                name = Path.GetFileNameWithoutExtension(path);
                return path;
            }

            if (File.Exists(Path.Join(path, "index.ebuild.cs")))
            {
                name = new DirectoryInfo(path).Name;
                return
                    Path.Join(path,
                        "index.ebuild.cs"); // index.ebuild.cs is the default file name or the most preferred one. for packages from internet.
            }

            if (File.Exists(Path.Join(path, new DirectoryInfo(path).Name + ".ebuild.cs")))
            {
                name = new DirectoryInfo(path).Name;
                return Path.Join(path,
                    new DirectoryInfo(path).Name +
                    ".ebuild.cs"); // package <name>.ebuild.cs is the second most preferred one.
            }

            if (File.Exists(Path.Join(path, "ebuild.cs")))
            {
                name = new DirectoryInfo(path).Name;
                return Path.Join(path, "ebuild.cs"); // ebuild.cs is the third most preferred one.
            }
            {
                var tryingPath = path + ".ebuild.cs";
                if (File.Exists(tryingPath))
                {
                    name = Path.GetFileNameWithoutExtension(path);
                    return Path.Join(tryingPath);
                }

            }

            name = string.Empty;
            return string.Empty;
        }
    }
}