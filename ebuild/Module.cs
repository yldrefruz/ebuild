using System.Runtime.InteropServices;

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
    public ModuleType Type;
    public string Name;

    private ModuleContext _moduleContext;

    public Module(ModuleContext context)
    {
        _moduleContext = context;
        Name = "unknown";

        CompilerName = null;
        ForceNamedCompiler = false;
        Architecture = RuntimeInformation.OSArchitecture;
        CppStandard = CXXStd.CXX20;

        //TODO: set a setting for this. or make the call from the child classes.
        AppendAutoDetectedSourceFiles();
    }


    public string[] GetFilesFromSourceDirectory(string searchPattern)
    {
        string directory = _moduleContext.ModuleDirectory;
        string sourceDir = Path.Join(directory, "Source");
        if (!Directory.Exists(sourceDir))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(sourceDir, searchPattern, SearchOption.AllDirectories);
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