using ebuild.api;
using ebuild.api.Compiler;
using ebuild.api.Linker;
using ebuild.BuildGraph;

namespace ebuild.Modules.BuildGraph;

class ModuleDeclarationNode : Node
{

    public ModuleBase Module;
    private static readonly Dictionary<string, ModuleDeclarationNode> _currentlyConstructing = new();

    public ModuleDeclarationNode(ModuleBase module) : base("ModuleDeclaration")
    {
        Module = module;

        // Check if this module is already being constructed (circular dependency)
        var moduleId = Module.Context.SelfReference.GetFilePath();
        if (_currentlyConstructing.ContainsKey(moduleId))
        {
            // This is a circular dependency, we'll add the reference later
            // Don't create compile/link nodes to avoid infinite recursion
            return;
        }

        _currentlyConstructing[moduleId] = this;

        try
        {
            AddDependencies(AccessLimit.Public);
            AddDependencies(AccessLimit.Private);
            // Source file compile nodes.
            var effectingChildren = GetEffectingDeclarations(this, false, true);
        foreach (var sourceFile in Module.SourceFiles)
        {
            CompilerBase compiler = Module.Context.InstancingParams!.Toolchain.CreateCompiler(Module, Module.Context.InstancingParams!).Result!;
            var compileNode = new CompileSourceFileNode(compiler, new CompilerSettings
            {
                SourceFile = sourceFile,
                OutputFile = Path.Join(CompilerUtils.GetObjectOutputFolder(Module), Path.GetFileNameWithoutExtension(sourceFile) + ".obj"),
                TargetArchitecture = Module.Context.TargetArchitecture,
                ModuleType = Module.Type,
                CPUExtension = Module.CPUExtension,
                EnableExceptions = Module.EnableExceptions,
                EnableFastFloatingPointOperations = Module.EnableFastFloatingPointOperations,
                EnableRTTI = Module.EnableRTTI,
                IsDebugBuild = Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
                EnableDebugFileCreation = Module.EnableDebugFileCreation ?? Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
                CppStandard = Module.CppStandard,
                CStandard = Module.CStandard,
                Definitions = [.. effectingChildren.SelectMany(v => v.Module.Definitions.Public), .. Module.Definitions.Joined(), .. Module.Context.Platform.GetPlatformDefinitions(Module)],
                IncludePaths = [.. effectingChildren.SelectMany(v => v.Module.Includes.Public), .. Module.Includes.Joined(), .. Module.Context.Platform.GetPlatformIncludes(Module)],
                ForceIncludes = [.. effectingChildren.SelectMany(v => v.Module.ForceIncludes.Public), .. Module.ForceIncludes.Joined()],
                Optimization = Module.OptimizationLevel,
                OtherFlags = [.. Module.CompilerOptions, .. Module.Context.Platform.GetPlatformCompilerFlags(Module)],
            });

            AddChild(compileNode, AccessLimit.Private);
        }
        // Link nodes.

        // first we need to create the input list for the linker.
        List<string> linkInputs = [];
        // Add the object files to the link inputs.
        Module.SourceFiles.ForEach(sf => linkInputs.Add(Path.Join(CompilerUtils.GetObjectOutputFolder(Module), Path.GetFileNameWithoutExtension(sf) + ".obj")));
        // Add the libraries to the link inputs.
        linkInputs.AddRange(Module.Libraries.Joined());
        linkInputs.AddRange(effectingChildren.SelectMany(v => v.Module.Libraries.Public));
        linkInputs.AddRange(Module.Context.Platform.GetPlatformLibraries(Module));

        List<string> libraryPaths = [];
        libraryPaths.AddRange(effectingChildren.SelectMany(v => v.Module.LibrarySearchPaths.Public));
        libraryPaths.AddRange(Module.LibrarySearchPaths.Joined());
        libraryPaths.AddRange(Module.Context.Platform.GetPlatformLibrarySearchPaths(Module));
        // Get the output binaries for the dependencies.
        foreach (var dependency in effectingChildren)
        {
            switch (dependency.Module.Type)
            {
                case ModuleType.StaticLibrary:
                    // For static libraries we link against the .lib file.
                    linkInputs.Add(dependency.Module.GetBinaryOutputPath());
                    break;
                case ModuleType.SharedLibrary:
                    // For shared libraries we need to link against the import/stub library.
                    linkInputs.Add(Path.ChangeExtension(dependency.Module.GetBinaryOutputPath(), Module.Context.Platform.ExtensionForStaticLibrary));
                    break;
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    // We don't link against executables.
                    break;
                default:
                    throw new NotImplementedException($"Unknown module type {dependency.Module.Type}");
            }
        }
        LinkerSettings linkerSettings = new()
        {
            InputFiles = [.. linkInputs],
            OutputFile = Module.GetBinaryOutputPath(),
            OutputType = Module.Type,
            TargetArchitecture = Module.Context.TargetArchitecture,
            LibraryPaths = [.. libraryPaths],
            LinkerFlags = [.. Module.Context.InstancingParams!.AdditionalLinkerOptions, .. Module.Context.Platform.GetPlatformLinkerFlags(Module)],
            ShouldCreateDebugFiles = Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
            IsDebugBuild = Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
        };
        var linker = Module.Context.Toolchain.CreateLinker(Module, Module.Context.InstancingParams!).Result!;
        var linkerNode = new LinkerNode(linker, linkerSettings);
        AddChild(linkerNode, AccessLimit.Private);
        //TODO: Additional Dependencies nodes.
        }
        finally
        {
            _currentlyConstructing.Remove(moduleId);
        }

    }

    private static List<ModuleDeclarationNode> GetEffectingDeclarations(ModuleDeclarationNode node, bool includeSelf = false, bool includePrivateChildren = true)
    {
        return GetEffectingDeclarations(node, includeSelf, includePrivateChildren, new HashSet<ModuleDeclarationNode>());
    }

    private static List<ModuleDeclarationNode> GetEffectingDeclarations(ModuleDeclarationNode node, bool includeSelf, bool includePrivateChildren, HashSet<ModuleDeclarationNode> visited)
    {
        // Prevent infinite recursion in case of circular dependencies
        if (visited.Contains(node))
        {
            return new List<ModuleDeclarationNode>();
        }

        visited.Add(node);
        
        var returnList = new List<ModuleDeclarationNode>();
        if (includeSelf)
            returnList.Add(node);
        foreach (var child in node.Children.GetLimited(AccessLimit.Public).OfType<ModuleDeclarationNode>())
        {
            returnList.AddRange(GetEffectingDeclarations(child, true, false, visited));
        }
        if (includePrivateChildren)
        {
            foreach (var child in node.Children.GetLimited(AccessLimit.Private).OfType<ModuleDeclarationNode>())
            {

                returnList.AddRange(GetEffectingDeclarations(child, true, false, visited));
            }
        }

        visited.Remove(node);
        return returnList;
    }

    private void AddDependencies(AccessLimit accessLimit)
    {
        foreach (var dependency in Module.Dependencies.GetLimited(accessLimit))
        {
            var moduleFile = ModuleFile.Get(dependency, Module.Context.SelfReference);
            var dependencyModuleId = moduleFile.GetFilePath();
            
            ModuleDeclarationNode childNode;
            
            // Check if this dependency is currently being constructed (circular dependency)
            if (_currentlyConstructing.ContainsKey(dependencyModuleId))
            {
                // Reuse the existing node to create the circular reference
                childNode = _currentlyConstructing[dependencyModuleId];
            }
            else
            {
                // Create a new instance and node
                var instance = moduleFile.CreateModuleInstance(Module.Context.InstancingParams!.CreateCopyFor(dependency)).Result!;
                childNode = new ModuleDeclarationNode(instance);
            }

            AddChild(childNode, accessLimit);
        }
    }

    public override string ToString() => $"- ModuleDeclarationNode({Module.Name})\n\tChildren: {string.Join(", ", Children.GetLimited(AccessLimit.Public).Select(c => c.ToString()))}";
}
