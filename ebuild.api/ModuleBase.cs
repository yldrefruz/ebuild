// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedParameter.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable PublicConstructorInAbstractClass
namespace ebuild.api;

public abstract class ModuleBase
{
    public ModuleBase(ModuleContext context)
    {
    }
    

    /// <summary>The definitions to use.</summary>
    public AccessLimitList<Definition> Definitions = new();

    /// <summary>Include directories to use.</summary>
    public AccessLimitList<IncludeDirectory> Includes = new();

    /// <summary> Forced include directories to use. </summary>
    public AccessLimitList<IncludeDirectory> ForceIncludes = new(); 

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

    /// <summary>The name of the module.</summary> 
    public string? Name;

    /// <summary>The cpp standard this module uses.</summary>
    public CppStandards CppStandard = CppStandards.Cpp20;

    /// <summary> The type of this module</summary>
    public ModuleType Type;

    /*
     * Functions to check support.
     */
    public virtual bool IsPlatformSupported(PlatformBase inPlatformBase) => true;

    public virtual bool IsCompilerSupported(CompilerBase inCompilerBase) => true;
}