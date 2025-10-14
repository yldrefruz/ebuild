using System.Security.Cryptography;
using ebuild.api;
using ebuild.api.Compiler;
using ebuild.api.Linker;
using ebuild.BuildGraph;
using Microsoft.Extensions.Logging;

namespace ebuild.Modules.BuildGraph;

class ModuleDeclarationNode : Node
{

    public ModuleBase Module;
    private static readonly Dictionary<string, ModuleDeclarationNode> _currentlyConstructing = new();

    ILogger Logger = EBuild.LoggerFactory.CreateLogger<ModuleDeclarationNode>();
    public ModuleDeclarationNode(ModuleBase module) : base("ModuleDeclaration")
    {
        Module = module;
        Name = $"ModuleDecl({Module.Name})";

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
            // prebuild steps
            foreach (var step in Module.PreBuildSteps)
            {
                var stepNode = new BuildStepNode(step, BuildStepNode.StepType.PreBuild);
                Logger.LogDebug("Adding pre-build step \"{StepName}\" to module \"{ModuleName}\"", stepNode.Name, Module.Name);
                AddChild(stepNode, AccessLimit.Private);
            }
            // Source file compile nodes.
            var effectingChildren = GetEffectingDeclarations(this, false, true);
            List<string> outFiles = [];
            bool shouldCompile = module.Type is not ModuleType.LibraryLoader;
            if (shouldCompile)
            {
                foreach (var sourceFile in Module.SourceFiles)
                {
                    if (Path.GetExtension(sourceFile) is ".h" or ".hpp" or ".inl")
                    {
                        // Skip header files.
                        continue;
                    }
                    var resourceSourceExtension = Module.Context.Platform.ExtensionForResourceSourceFile;
                    var isResourceFile = Path.GetExtension(sourceFile) == Module.Context.Platform.ExtensionForResourceSourceFile;
                    CompilerBase compiler = isResourceFile ? Module.Context.InstancingParams!.Toolchain.CreateResourceCompiler(Module, Module.Context.InstancingParams!).Result! : Module.Context.InstancingParams!.Toolchain.CreateCompiler(Module, Module.Context.InstancingParams!).Result!;
                    if (compiler == null)
                    {
                        if (isResourceFile)
                        {
                            // skip resource files if no resource compiler is available.
                            Console.WriteLine($"Skipping resource file {sourceFile} as no resource compiler is available in toolchain {Module.Context.Toolchain.Name}");
                            continue;
                        }
                        else
                        {
                            throw new Exception($"No compiler available for module {Module.Name} in toolchain {Module.Context.Toolchain.Name}");
                        }
                    }
                    List<Definition> sourceFileDefinitions = [.. effectingChildren.SelectMany(v => v.Module.Definitions.Public), .. Module.Definitions.Joined(), .. Module.Context.Platform.GetPlatformDefinitions(Module)];
                    List<string> sourceFileIncludePaths = [.. effectingChildren.SelectMany(v => v.Module.Includes.Public.Select(c => Path.GetFullPath(c, v.Module.Context.ModuleDirectory.FullName))), .. Module.Includes.Joined().Select(c => Path.GetFullPath(c, Module.Context.ModuleDirectory.FullName)), .. Module.Context.Platform.GetPlatformIncludes(Module)];

                    List<Definition> resourceFileDefinitions = [.. effectingChildren.SelectMany(v => v.Module.ResourceDefinitions.Public), .. Module.ResourceDefinitions.Joined()];
                    List<string> resourceFileIncludePaths = [.. effectingChildren.SelectMany(v => v.Module.ResourceIncludes.Public.Select(c => Path.GetFullPath(c, v.Module.Context.ModuleDirectory.FullName))), .. Module.ResourceIncludes.Joined().Select(c => Path.GetFullPath(c, Module.Context.ModuleDirectory.FullName))];
                    var outputFile = Path.Join(CompilerUtils.GetObjectOutputFolder(Module), Path.GetFileNameWithoutExtension(sourceFile) + (isResourceFile ? Module.Context.Platform.ExtensionForCompiledResourceFile : Module.Context.Platform.ExtensionForCompiledSourceFile));
                    outFiles.Add(outputFile);
                    var compileSettings = new CompilerSettings
                    {
                        SourceFile = Path.GetFullPath(sourceFile, Module.Context.ModuleDirectory.FullName),
                        OutputFile = outputFile,
                        TargetArchitecture = Module.Context.TargetArchitecture,
                        ModuleType = Module.Type,
                        IntermediateDir = CompilerUtils.GetObjectOutputFolder(Module),
                        CPUExtension = Module.CPUExtension,
                        EnableExceptions = Module.EnableExceptions,
                        EnableFastFloatingPointOperations = Module.EnableFastFloatingPointOperations,
                        EnableRTTI = Module.EnableRTTI,
                        IsDebugBuild = Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
                        EnableDebugFileCreation = Module.EnableDebugFileCreation ?? Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
                        CppStandard = Module.CppStandard,
                        CStandard = Module.CStandard,
                        Definitions = isResourceFile ? resourceFileDefinitions : sourceFileDefinitions,
                        IncludePaths = isResourceFile ? resourceFileIncludePaths : sourceFileIncludePaths,
                        ForceIncludes = [.. effectingChildren.SelectMany(v => v.Module.ForceIncludes.Public.Select(c => Path.GetFullPath(c, v.Module.Context.ModuleDirectory.FullName))), .. Module.ForceIncludes.Joined().Select(c => Path.GetFullPath(c, Module.Context.ModuleDirectory.FullName))],
                        Optimization = Module.OptimizationLevel,
                        OtherFlags = [.. Module.CompilerOptions, .. Module.Context.Platform.GetPlatformCompilerFlags(Module)],
                    };
                    var compileNode = new CompileSourceFileNode(compiler!, compileSettings);

                    AddChild(compileNode, AccessLimit.Private);
                }
            }
            // Link nodes.
            // There is nothing to link for library loader modules. They just provide the libraries to link with their Libraries.Public.
            bool shouldLink = module.Type is not ModuleType.LibraryLoader;
            if (shouldLink)
            {
                // first we need to create the input list for the linker.
                List<string> linkInputs = [];
                // Add the object files to the link inputs.
                linkInputs.AddRange(outFiles.Select(sf =>
                {
                    var isResourceFile = Path.GetExtension(sf) == Module.Context.Platform.ExtensionForResourceSourceFile;
                    var file = Path.Join(CompilerUtils.GetObjectOutputFolder(Module), Path.GetFileNameWithoutExtension(sf) + (isResourceFile ? Module.Context.Platform.ExtensionForCompiledResourceFile : Module.Context.Platform.ExtensionForCompiledSourceFile));
                    return Path.GetFullPath(file, Module.Context.ModuleDirectory.FullName);
                }));
                // Add the libraries to the link inputs.
                linkInputs.AddRange(Module.Libraries.Joined());
                linkInputs.AddRange(effectingChildren.SelectMany(v => v.Module.Libraries.Public));
                linkInputs.AddRange(Module.Context.Platform.GetPlatformLibraries(Module));

                List<string> libraryPaths = [];
                libraryPaths.AddRange(effectingChildren.SelectMany(v => v.Module.LibrarySearchPaths.Public));
                libraryPaths.AddRange(Module.LibrarySearchPaths.Joined());
                libraryPaths.AddRange(Module.Context.Platform.GetPlatformLibrarySearchPaths(Module));
                // Get the output binaries for the dependencies.
                foreach (var dependency in Children.Joined().Where(a => a is ModuleDeclarationNode).Select(a => (ModuleDeclarationNode)a))
                {
                    switch (dependency.Module.Type)
                    {
                        case ModuleType.StaticLibrary:
                            // For static libraries we link against the .lib file.
                            linkInputs.Add(dependency.Module.GetBinaryOutputPath());
                            break;
                        case ModuleType.SharedLibrary:
                            // For shared libraries we need to link against the import/stub library if we are on Windows.
                            if (Module.Context.Platform.Name == "windows")
                            {
                                linkInputs.Add(Path.ChangeExtension(dependency.Module.GetBinaryOutputPath(), Module.Context.Platform.ExtensionForStaticLibrary));
                            }
                            else
                            {
                                linkInputs.Add(dependency.Module.GetBinaryOutputPath());
                            }
                            break;
                        case ModuleType.Executable:
                        case ModuleType.ExecutableWin32:
                            // We don't link against executables.
                            break;
                        case ModuleType.LibraryLoader:
                            // We don't link against library loaders.
                            // We link against their public children instead. Which is handled by GetEffectingDeclarations.
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
                    IntermediateDir = CompilerUtils.GetObjectOutputFolder(Module),
                    LibraryPaths = [.. libraryPaths],
                    LinkerFlags = [.. Module.Context.InstancingParams!.AdditionalLinkerOptions, .. Module.Context.Platform.GetPlatformLinkerFlags(Module)],
                    ShouldCreateDebugFiles = Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
                    IsDebugBuild = Module.Context.Configuration.Equals("debug", StringComparison.InvariantCultureIgnoreCase),
                    DelayLoadLibraries = [.. Module.DelayLoadLibraries]
                };
                var linker = Module.Context.Toolchain.CreateLinker(Module, Module.Context.InstancingParams!).Result!;
                var linkerNode = new LinkerNode(linker, linkerSettings);
                AddChild(linkerNode, AccessLimit.Private);
            }

            //TODO: Additional Dependencies nodes.
            // postbuild steps
            foreach (var step in Module.PostBuildSteps)
            {
                var stepNode = new BuildStepNode(step, BuildStepNode.StepType.PostBuild);
                Logger.LogDebug("Adding post-build step \"{StepName}\" to module \"{ModuleName}\"", stepNode.Name, Module.Name);
                AddChild(stepNode, AccessLimit.Private);
            }
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
