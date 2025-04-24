using ebuild.api;
using ebuild.Compilers;

namespace ebuild;

public class BuildGraph
{
    public abstract class Node
    {
        public abstract string GetDisplayName();
        public List<Node> Dependencies { get; set; } = new List<Node>();
        public bool IsBuilt { get; set; } = false;
    }

    public class LinkLibraryNode : Node
    {
        public string LibraryPath { get; set; }
        public ModuleReference OwnerModule { get; set; }

        public LinkLibraryNode(string libraryPath, ModuleReference ownerModule)
        {
            LibraryPath = libraryPath;
            OwnerModule = ownerModule;
        }

        public override string GetDisplayName()
        {
            return $"Load {LibraryPath}";
        }
    }

    public class AdditionalDependencyNode : Node
    {
        public AdditionalDependency Dependency { get; set; }
        public ModuleReference OwnerModule { get; set; }
        public AdditionalDependencyNode(AdditionalDependency dependency, ModuleReference ownerModule)
        {
            Dependency = dependency;
            OwnerModule = ownerModule;
        }
        public override string GetDisplayName()
        {
            return $"AdditionalDependency {Dependency.DependencyPath}->{Dependency.TargetDirectory}";
        }
    }

    public class CompileFileNode : Node
    {
        public string FilePath { get; set; }
        public string CompilationResultPath { get; set; }
        public string CompilationPDBPath { get; set; }
        public CompilerBase Compiler { get; set; }
        public ModuleReference OwnerModule { get; set; }

        public CompileFileNode(string filePath, ModuleReference ownerModule, string compilationResultPath,
         string compilationPDBPath, CompilerBase compiler)
        {
            FilePath = filePath;
            OwnerModule = ownerModule;

            CompilationResultPath = compilationResultPath;
            CompilationPDBPath = compilationPDBPath;
            Compiler = compiler;
        }

        public override string GetDisplayName()
        {
            return $"Compile {FilePath}";
        }

        public string GetCompilerArguments(){
            
        }
    }

    public class LinkFileNode : Node
    {
        public string LinkerName { get; set; }
        public string LinkResultPath { get; set; }
        public string LinkerPDBPath { get; set; }
        public ModuleReference OwnerModule { get; set; }

        public LinkFileNode(string linkResultPath, ModuleReference ownerModule, string linkerName, string linkerPDBPath)
        {
            LinkResultPath = linkResultPath;
            OwnerModule = ownerModule;
            LinkerName = linkerName;
            LinkerPDBPath = linkerPDBPath;
        }

        public override string GetDisplayName()
        {
            return $"Link {LinkResultPath}";
        }
    }

}