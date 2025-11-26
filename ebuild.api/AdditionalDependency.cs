namespace ebuild.api
{
    /// <summary>
    /// Represents an additional dependency for a module. This can be either a file or a directory.
    /// </summary>
    public class AdditionalDependency
    {
        /// <summary>
        /// The type of the dependency.
        /// </summary>
        public enum DependencyType
        {
            /// <summary>
            /// This dependency is a file. The file will be processed. Default operation is copy.
            /// </summary>
            File,
            /// <summary>
            /// This dependency is a directory. The contents of this directory will be copied.
            /// </summary>
            Directory
        }
        /// <summary>
        /// The type of the dependency.
        /// </summary>
        public DependencyType Type { get; private set; }

        /// <summary>
        /// The path to the dependency. This can be either a file or a directory.
        /// The path must be absolute.
        /// </summary>
        public string DependencyPath { get; private set; }

        /// <summary>
        /// The target path where the dependency should be copied to.
        /// If null, the dependency will be copied to the same location as the source, but in the output directory.
        /// The path must be absolute. 
        /// </summary>
        public string? TargetPath { get; private set; } = "$(RuntimeDir)/$(FileName)";
        private ModuleBase? _ownerModule;
        /// <summary>
        /// The owner module for this dependency.
        /// </summary>
        public ModuleBase OwnerModule { get => GetOwnerModule(); set => SetOwnerModule(value); }
        /// <summary>
        /// A custom processor function that can be used to process the dependency before copying it.
        /// Blocks the current thread.
        /// </summary>
        public CustomProcessor? Processor { get; }

        /// <summary>
        /// Gets the file name of the dependency if it is a file. If it is a directory, returns an empty string.
        /// </summary>
        /// <returns>The file name of the dependency if it is a file; otherwise, an empty string.</returns>
        /// <exception cref="ArgumentException">Specified DependencyType is not supported</exception>
        public string GetDependencyFileName()
        {
            return Type switch
            {
                DependencyType.File => Path.GetFileName(DependencyPath),
                DependencyType.Directory => string.Empty,
                _ => throw new ArgumentException("Invalid DependencyType"),
            };
        }
        /// <summary>
        /// Resolves macros in the given path.
        /// </summary>
        /// <param name="path">Input path to resolve macros in</param>
        /// <param name="rootModule">The root module that tries to resolve this</param>
        /// <param name="usedRuntimeDir">Outputs whether the $(RuntimeDir) macro was used</param>
        /// <returns>The resolved macros string</returns>
        public string ResolveMacros(string path, ModuleBase rootModule, out bool usedRuntimeDir)
        {
            usedRuntimeDir = path.Contains("$(RuntimeDir)");
            return Path.TrimEndingDirectorySeparator(path
                .Replace("$(ModuleOutputDir)", GetOwnerModule().GetBinaryOutputDirectory())
                .Replace("$(RootModuleOutputDir)", rootModule.GetBinaryOutputDirectory())
                .Replace("$(RuntimeDir)", rootModule.GetBinaryOutputDirectory())
                .Replace("$(FileName)", GetDependencyFileName())
                .Replace("$(FileNameNoExt)", Path.GetFileNameWithoutExtension(GetDependencyFileName())));
        }

        private void SetOwnerModule(ModuleBase module)
        {
            _ownerModule = module;
        }

        private ModuleBase GetOwnerModule()
        {
            return _ownerModule ?? throw new NullReferenceException("OwnerModule is not set.");
        }

        /// <summary>
        /// Create a dependency from a file or directory path.
        /// Default action is to copy to the Runtime Output Directory.
        /// </summary>
        /// <param name="dependencyPath">The dependency path.</param>
        public AdditionalDependency(string dependencyPath)
        {
            DependencyPath = dependencyPath;

        }
        internal void ResolveDependency()
        {
            DependencyPath = Path.GetFullPath(DependencyPath, GetOwnerModule().Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory());
            if (Directory.Exists(DependencyPath))
            {
                this.Type = DependencyType.Directory;
            }
            else if (File.Exists(DependencyPath))
            {
                this.Type = DependencyType.File;
            }
        }
        /// <summary>
        /// Create a dependency from a file or directory path with a custom processor.
        /// </summary>
        /// <param name="dependencyFile"></param>
        /// <param name="processor"></param>
        public AdditionalDependency(string dependencyFile, CustomProcessor processor) : this(dependencyFile)
        {
            Processor = processor;
        }
        /// <summary>
        /// Create a dependency from a file or directory path with a target directory.
        /// There are macros available for target directory.
        /// - $(ModuleOutputDir): The output directory of the owner module.
        /// - $(RootModuleOutputDir): The output directory of the root module.
        /// - $(RuntimeDir): The output directory of the root module (only if it is an executable type, otherwise the default processor fails silently).
        /// - $(FileName): The file name of the dependency (only for file dependencies).
        /// - $(FileNameNoExt): The file name of the dependency without extension (only for file dependencies).
        /// </summary>
        /// <param name="dependencyFile">The file or directory path of the dependency.</param>
        /// <param name="targetDir">The target directory where the dependency should be copied to. Macros can be used here.</param>
        public AdditionalDependency(string dependencyFile, string targetDir) : this(dependencyFile)
        {
            TargetPath = targetDir;
        }
        /// <summary>
        /// Create a dependency from a file or directory path with a target directory and a custom processor.
        /// There are macros available for target directory.
        /// - $(ModuleOutputDir): The output directory of the owner module.
        /// - $(RootModuleOutputDir): The output directory of the root module.
        /// - $(RuntimeDir): The output directory of the root module (only if it is an executable type, otherwise the default processor fails silently).
        /// - $(FileName): The file name of the dependency (only for file dependencies).
        /// - $(FileNameNoExt): The file name of the dependency without extension (only for file dependencies).
        /// </summary>
        /// <param name="dependencyFile">The file or directory path of the dependency.</param>
        /// <param name="targetDir">The target directory where the dependency should be copied to. Macros can be used here.</param>
        /// <param name="processor">A custom processor to handle the dependency.</param>
        public AdditionalDependency(string dependencyFile, string targetDir, CustomProcessor processor) : this(dependencyFile, processor)
        {
            TargetPath = targetDir;
        }
        /// <summary>
        /// A custom processor delegate to process the dependency.
        /// There are macros available for target path.
        /// - $(ModuleOutputDir): The output directory of the owner module.
        /// - $(RootModuleOutputDir): The output directory of the root module.
        /// - $(RuntimeDir): The output directory of the root module (only if it is an executable type, otherwise the default processor fails silently).
        /// - $(FileName): The file name of the dependency (only for file dependencies).
        /// - $(FileNameNoExt): The file name of the dependency without extension (only for file dependencies).
        /// </summary>
        /// <param name="dependencyPath">The path to dependency</param>
        /// <param name="targetPath">The path to target. This will have its macros resolved</param>
        public delegate void CustomProcessor(string dependencyPath, string targetPath);

        /// <summary>
        /// Converts the AdditionalDependency to a string representation.
        /// </summary>
        /// <returns>the string representation</returns>
        /// <exception cref="ArgumentOutOfRangeException">If the DependencyType is not valid.</exception>
        public override string ToString()
        {
            return Type switch
            {
                DependencyType.File => $"AdditionalDependency: File {DependencyPath} -> {TargetPath + Path.GetFileName(DependencyPath)}" + (Processor != null ? " with custom processor" : ""),
                DependencyType.Directory => $"AdditionalDependency: Directory {DependencyPath} -> {TargetPath}" + (Processor != null ? " with custom processor" : ""),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
        static private void CopyDirectory(string dirPath, string targetPath)
        {
            foreach (var dir in Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dir.Replace(dirPath, targetPath));
            }

            foreach (var file in Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(file, file.Replace(dirPath, targetPath), true);
            }
        }
        /// <summary>
        /// Processes the additional dependency by copying it to the target location if no CustomProcessor is specified.
        /// If specified, resolves the targets then calls the CustomProcessor.
        /// </summary>
        /// <param name="RootModule"></param>
        public void Process(ModuleBase RootModule)
        {
            DependencyPath = Path.GetFullPath(DependencyPath, GetOwnerModule().Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory());
            TargetPath = ResolveMacros(TargetPath ?? "", RootModule, out var usedRuntimeDir);
            if (usedRuntimeDir && RootModule.Type is not (ModuleType.Executable or ModuleType.ExecutableWin32))
            {
                // If the root module is not an executable, we cannot use the runtime dir.
                // Fail silently.
                // Console.WriteLine($"[Warning] AdditionalDependency: The root module '{RootModule.Name}' is not an executable. Skipping dependency '{DependencyPath}' processing.");
                return;
            }
            switch (Type)
            {
                case DependencyType.Directory:
                    {
                        var targetDir = TargetPath ?? GetOwnerModule().GetBinaryOutputDirectory();
                        Directory.CreateDirectory(targetDir);
                        if (Processor == null)
                        {
                            CopyDirectory(DependencyPath, targetDir);
                        }
                        else
                        {
                            Processor(DependencyPath, targetDir);
                        }
                        break;
                    }
                case DependencyType.File:
                    {
                        var targetDir = Path.GetDirectoryName(TargetPath) ?? throw new InvalidOperationException("TargetPath directory could not be determined.");
                        Directory.CreateDirectory(targetDir);
                        var targetFile = TargetPath;
                        if (File.Exists(targetFile)) // TODO: clean compilation should overwrite.
                        {
                            if (File.GetLastWriteTimeUtc(targetFile) >= File.GetLastWriteTimeUtc(DependencyPath))
                            {
                                break;
                            }
                        }
                        if (Processor == null)
                        {
                            File.Copy(DependencyPath, targetFile, true);
                        }
                        else
                        {
                            Processor(DependencyPath, targetFile);
                        }

                        break;
                    }
            }
        }
    }
}