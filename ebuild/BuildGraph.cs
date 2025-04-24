using System.Security.Cryptography.X509Certificates;
using ebuild.api;

namespace ebuild;
public class BuildGraph
{
    public BuildGraph(ModuleBase OwnerModule)
    {
        _rootNode = new ModuleNode(ModuleFile.Get(OwnerModule.Context.SelfReference), AccessLimit.Public);
        OwnerModule.SourceFiles.ForEach((string filePath) =>
        {
            _rootNode.AddDirectChild(new SourceNode((ModuleFile)OwnerModule.Context.SelfReference, filePath, AccessLimit.Public));
        });
        OwnerModule.Dependencies.Public.ForEach((ModuleReference dependency) =>
        {
            _rootNode.AddDirectChild(new ModuleNode(ModuleFile.Get(dependency), AccessLimit.Public));
        });
        OwnerModule.Dependencies.Private.ForEach((ModuleReference dependency) =>
        {
            _rootNode.AddDirectChild(new ModuleNode(ModuleFile.Get(dependency), AccessLimit.Private));
        });
    }

    public abstract class Node
    {
        public virtual IEnumerable<Node> GetChildren(AccessLimit? limit = null) => Children;
        public void AddDirectChild(Node children)
        {
            if (!Children.Contains(children))
            {
                Children.Add(children);
            }
        }
        public void AddRangeDirectChildren(IEnumerable<Node> children)
        {
            foreach (var child in children)
            {
                if (!Children.Contains(child))
                {
                    Children.Add(child);
                }
            }
        }
        protected List<Node> Children = [];
        public AccessLimit limit = AccessLimit.Public;
    }

    public class ModuleNode : Node
    {
        public ModuleNode(ModuleFile module, AccessLimit limit = AccessLimit.Public)
        {
            this.limit = limit;
            this.Module = module;
        }
        // 
        public ModuleFile Module;

        public override IEnumerable<Node> GetChildren(AccessLimit? limit = null)
        {
            if (limit != null)
                return Module.GetBuildGraph()?._rootNode.GetChildren().Where((Node n) => n.limit == limit) ?? [];
            return Module.GetBuildGraph()?._rootNode.GetChildren() ?? [];
        }
    }

    public class SourceNode : Node
    {
        public SourceNode(ModuleFile ownerModule, string filePath, AccessLimit limit = AccessLimit.Public)
        {
            this.limit = limit;
            this.FilePath = filePath;
            this.OutputPath = Path.Combine(ownerModule.GetCompiledModule().Context.ModuleDirectory!.FullName,
             ownerModule.GetCompiledModule().OutputDirectory,
             Path.GetFileNameWithoutExtension(filePath) + ownerModule.GetCompiledModule().Context.Compiler == "MSVC" ? ".obj" : ".o");
        }
        public string FilePath;
        public bool isBuilt = false;
        public string OutputPath = string.Empty;
    }

    public class LinkLibraryNode : Node
    {
        public LinkLibraryNode(string filePath, AccessLimit limit = AccessLimit.Public)
        {
            this.limit = limit;
            this.FilePath = filePath;

        }
        public string FilePath; // The library to link to the final product.
    }

    public class AdditionalDependencyNode : Node
    {
        public AdditionalDependencyNode(AdditionalDependency additionalDependency, AccessLimit limit = AccessLimit.Public)
        {
            this.limit = limit;
            this.dependency = additionalDependency;
        }
        public AdditionalDependency dependency; // The library to link to the final product.
    }

    private ModuleNode _rootNode;
}