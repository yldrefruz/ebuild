using System.Runtime.InteropServices;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable ConvertToConstant.Global

namespace ebuild;

public class Module
{
    public string? CompilerName;
    public bool ForceNamedCompiler;
    public readonly Architecture Architecture;
    public List<string> Includes = new(new[] { "./Source/Public", "./Source/Private", "./Source" });
    public List<string> SourceFiles = new();
    public List<string> Definitions = new();
    public List<string> Libraries = new();
    public List<string> LibrarySearchPaths = new();
    public List<ModuleDependency> ModuleDependencies = new();
    public List<AdditionalDependency> AdditionalDependencies = new();
    public CXXStd CppStandard;

    public bool UseDefaultIncludes = true;
    public bool UseDefaultLibraryPaths = true;
    public bool UseDefaultLibraries = true;
    public bool AddDefaultSourcePaths = true;
    public ModuleType Type;
    public string Name;

    private ModuleContext _moduleContext;

    public Module(ModuleContext context)
    {
        _moduleContext = context;
        Name = "unknown";
        Type = ModuleType.Executable;

        CompilerName = null;
        ForceNamedCompiler = false;
        Architecture = RuntimeInformation.OSArchitecture;
        CppStandard = CXXStd.CXX20;

        if (AddDefaultSourcePaths)
            AppendAutoDetectedSourceFiles();
    }


    public IEnumerable<string> GetFilesFromSourceDirectory(string searchPattern)
    {
        var directory = _moduleContext.ModuleDirectory;
        var sourceDir = Path.Join(directory, "Source");
        return !Directory.Exists(sourceDir)
            ? Array.Empty<string>()
            : Directory.GetFiles(sourceDir, searchPattern, SearchOption.AllDirectories);
    }

    public void AppendAutoDetectedSourceFiles()
    {
        List<string> files = new();
        files.AddRange(GetFilesFromSourceDirectory("*.cpp"));
        //files.AddRange(GetFilesFromSourceDirectory("*.hpp"));
        //files.AddRange(GetFilesFromSourceDirectory("*.h"));
        files.AddRange(GetFilesFromSourceDirectory("*.c"));
        //files.AddRange(GetFilesFromSourceDirectory("*.inl"));

        SourceFiles.AddRange(files);
    }
}