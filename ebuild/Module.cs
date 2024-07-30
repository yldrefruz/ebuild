using System.Runtime.InteropServices;

namespace ebuild;

public class Module
{
    public string? CompilerName;
    public bool ForceNamedCompiler;
    public Architecture Architecture;
    public List<string> Includes = ["./Public"];
    public List<string> SourceFiles = [];
    public List<string> Definitions = [];
    public List<string> Libraries = [];
    public List<string> LibrarySearchPaths = [];
    public bool UseDefaultIncludes = true;
    public ModuleType Type;
    public string Name;

    public Module(ModuleContext context)
    {
        Name = "unknown";
        CompilerName = null;
        ForceNamedCompiler = false;
        Architecture = RuntimeInformation.OSArchitecture;
    }
}