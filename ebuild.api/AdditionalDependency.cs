namespace ebuild.api;

/// <summary>
/// Represents an additional dependency for a module. This can be either a file or a directory.
/// 
/// Points to improve:
/// - The dependency copy strategy. 
/// - Dependent modules copy strategy. How dependent modules should copy the dependencies.
/// - Unnecessary copying of dependencies. If a dependency is already copied, it should not be copied again. Also compilation for dependent modules should not be copied as it will increase the compilation time incredibly.
/// </summary>
public class AdditionalDependency
{
    public enum DependencyType
    {
        File,
        Directory
    }

    public DependencyType Type { get; }

    /// <summary>
    /// The path to the dependency. This can be either a file or a directory.
    /// The path must be absolute.
    /// </summary>
    public string DependencyPath { get; }

    /// <summary>
    /// The target path where the dependency should be copied to.
    /// If null, the dependency will be copied to the same location as the source, but in the output directory.
    /// The path must be absolute. 
    /// 
    /// 
    /// Macros are supported in the path. The following macros are available:
    /// - ${RootOutputDir} - Absolute path of the output directory of the root module.
    /// - ${OutputDir} - Absolute path of the output directory of the module that owns this dependency.
    /// </summary>
    public string? TargetDirectory { get; private set;} = "${RootOutputDir}";

    private ModuleBase? OwnerModule;
    public void SetOwnerModule(ModuleBase module)
    {
        OwnerModule = module;
    }

    public CustomProcessor? Processor { get; }

    public AdditionalDependency(string dependencyFile)
    {
        DependencyPath = dependencyFile;
        if (Directory.Exists(dependencyFile))
        {
            Type = DependencyType.Directory;
        }
        else if (File.Exists(dependencyFile))
        {
            Type = DependencyType.File;
        }
        else
        {
            throw new ArgumentException($"Invalid dependency {dependencyFile}, couldn't be found or detect the type");
        }
    }

    public AdditionalDependency(string dependencyFile, CustomProcessor processor) : this(dependencyFile)
    {
        Processor = processor;
    }

    public AdditionalDependency(string dependencyFile, string targetDir) : this(dependencyFile)
    {
        TargetDirectory = targetDir;
    }

    public AdditionalDependency(string dependencyFile, string targetDir, CustomProcessor processor) : this(dependencyFile, processor)
    {
        TargetDirectory = targetDir;
    }

    public delegate void CustomProcessor(string dependencyPath, string targetDir);


    public override string ToString()
    {
        switch (Type)
        {
            case DependencyType.File:
                return $"AdditionalDependency: File {DependencyPath} -> {TargetDirectory + Path.GetFileName(DependencyPath)}" + (Processor != null ? " with custom processor" : "");
            case DependencyType.Directory:
                return $"AdditionalDependency: Directory {DependencyPath} -> {TargetDirectory}" + (Processor != null ? " with custom processor" : "");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Process(ModuleBase RootModule)
    {
        if (OwnerModule == null)
            throw new NullReferenceException("OwnerModule must be set before processing the dependency.");
        TargetDirectory = TargetDirectory?.Replace("$(OutputDir)", OwnerModule.GetBinaryOutputDirectory()) ?? OwnerModule.GetBinaryOutputDirectory();
        switch (Type)
        {
            case DependencyType.Directory:
                {
                    var targetDir = TargetDirectory ?? Path.Join(OwnerModule.Context.ModuleDirectory!.FullName, OwnerModule.GetBinaryOutputDirectory());
                    Directory.CreateDirectory(targetDir); // Create the target directory if it doesn't exist
                    foreach (var file in Directory.GetFiles(DependencyPath, "*", SearchOption.AllDirectories))
                    {
                        var targetFile = Path.Combine(targetDir, Path.GetFileName(DependencyPath));
                        if (Processor == null)
                        {
                            File.Copy(file, targetFile, true);
                        }
                        else
                        {
                            Processor(DependencyPath, targetFile);
                        }
                    }

                    break;
                }
            case DependencyType.File:
                {
                    var targetDir = TargetDirectory ?? Path.Join(OwnerModule.Context.ModuleDirectory!.FullName, OwnerModule.GetBinaryOutputDirectory());
                    Directory.CreateDirectory(targetDir);
                    var targetFile = Path.Combine(targetDir, Path.GetFileName(DependencyPath));
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