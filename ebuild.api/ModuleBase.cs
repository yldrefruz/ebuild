using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public abstract class ModuleBase(ModuleContext context)
{
    /// <summary>The definitions to use.</summary>
    public AccessLimitList<Definition> Definitions = new();

    /// <summary>Include directories to use.</summary>
    public AccessLimitList<string> Includes = new();

    /// <summary> Forced include directories to use. </summary>
    public AccessLimitList<string> ForceIncludes = new();

    // TODO: ability to add CMake targets as modules
    /// <summary>Other modules to depend on (ebuild modules.)</summary> 
    public AccessLimitList<ModuleReference> Dependencies = new();

    /// <summary>Dependencies to add for this module. These are copied to the build directory.</summary>
    public AccessLimitList<AdditionalDependency> AdditionalDependencies = new();

    /// <summary>Additional compiler options to add.</summary>
    public AccessLimitList<string> Options = new();

    /// <summary>The libraries to link.</summary>
    public AccessLimitList<string> Libraries = new();

    /// <summary>The library paths to search for. Absolute or relevant</summary>
    public AccessLimitList<string> LibrarySearchPaths = new();

    public List<string> SourceFiles = new();

    /// <summary>The name of the module. If null will automatically deduce the name from the file name.</summary> 
    public string? Name;

    /// <summary>The cpp standard this module uses.</summary>
    public CppStandards CppStandard = CppStandards.Cpp20;

    /// <summary> The type of this module</summary>
    public ModuleType Type;

    public ModuleContext Context = context;

    /*
     * Functions to check support.
     */
    public virtual bool IsPlatformSupported(PlatformBase inPlatformBase) => true;

    public virtual bool IsCompilerSupported(CompilerBase inCompilerBase) => true;

    public virtual bool IsArchitectureSupported(Architecture architecture) => true;
}