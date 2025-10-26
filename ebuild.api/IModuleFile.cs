namespace ebuild.api
{
    /// <summary>
    /// Represents an abstraction over a module file on disk. Implementations provide
    /// the ability to create an instantiated <see cref="ModuleBase"/>, inspect
    /// compiled module state, and query file-system related paths.
    /// </summary>
    public interface IModuleFile
    {
        /// <summary>
        /// Instantiate the module described by this module file.
        /// </summary>
        /// <param name="instancingParams">Parameters used to create the module (context, options, etc.).
        /// See <see cref="ModuleFile.CreateModuleInstance(IModuleInstancingParams)"/> for a concrete example of how this is used.
        /// </param>
        /// <returns>
        /// A task that resolves to the created <see cref="ModuleBase"/> instance or <c>null</c> if the module could not be instantiated.
        /// Implementations may log messages via the module context; callers should handle a <c>null</c> result.
        /// </returns>
        public Task<ModuleBase?> CreateModuleInstance(IModuleInstancingParams instancingParams);

        /// <summary>
        /// Returns the compiled module instance if it has already been created/compiled; otherwise <c>null</c>.
        /// </summary>
        /// <returns>The compiled <see cref="ModuleBase"/> or <c>null</c> when not compiled/instantiated yet.</returns>
        public ModuleBase? GetCompiledModule();

        /// <summary>
        /// Convenience helper returning whether a compiled module instance exists.
        /// Equivalent to <c>GetCompiledModule() != null</c>.
        /// </summary>
        /// <returns><c>true</c> when a compiled module instance is available; otherwise <c>false</c>.</returns>
        public bool IsCompiled() => GetCompiledModule() != null;

        /// <summary>
        /// Returns the <see cref="ModuleReference"/> that identifies this module file (output type, path, version, options).
        /// </summary>
        /// <returns>The module's self reference.</returns>
        public ModuleReference GetSelfReference();

        /// <summary>
        /// Returns the full file-system path to the module file backing this instance.
        /// </summary>
        /// <returns>The path to the module file.</returns>
        public string GetFilePath();

        /// <summary>
        /// Returns the directory that contains the module file. This is typically the parent
        /// directory of the file returned by <see cref="GetFilePath"/>.
        /// </summary>
        /// <returns>The directory path that contains the module file.</returns>
        public string GetDirectory();

        /// <summary>
        /// Indicates whether the on-disk module file (source) has changed since the last compilation
        /// or cached assembly was produced. Implementations typically compare timestamps of the
        /// source file and the cached compiled DLL.
        /// </summary>
        /// <returns><c>true</c> if the module source is newer than the cached assembly or the cache is missing; otherwise <c>false</c>.</returns>
        public bool HasChanged();

        /// <summary>
        /// Attempts to resolve a path (or directory) to a concrete module file path. This helper
        /// implements the lookup precedence used by the tooling:
        ///
        /// - If <paramref name="path"/> points to an existing file it is returned.
        /// - Otherwise, the method checks the following candidate files (in order) inside the directory named by <paramref name="path"/>:
        ///   1. <c>index.ebuild.cs</c>
        ///   2. <c>{directoryName}.ebuild.cs</c> (where <c>{directoryName}</c> is the directory's name)
        ///   3. <c>ebuild.cs</c>
        /// - Finally, if none of the above exist it will also test <c>{path}.ebuild.cs</c> (treating <paramref name="path"/> as a file base name).
        ///
        /// The method returns the resolved file path and sets <paramref name="name"/> to a short module name derived from the chosen file
        /// (typically the file name without extension or the directory name). If no candidate is found the method returns an empty string and
        /// sets <paramref name="name"/> to <c>string.Empty</c>.
        /// </summary>
        /// <param name="path">A path to a file or a directory to search for a module file.</param>
        /// <param name="name">Out parameter that receives the short module name (file base name or directory name) when successful; otherwise an empty string.</param>
        /// <returns>The resolved path to the module file, or an empty string if none was found.</returns>
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