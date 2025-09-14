using System.Reflection;

namespace ebuild.api.Linker;

public abstract class LinkerBase(ModuleBase module, IModuleInstancingParams instancingParams)
{
    /// <summary>
    /// The module we are currently working to link
    /// </summary>
    protected ModuleBase? CurrentModule = module;
    public IModuleInstancingParams InstancingParams = instancingParams;

    public readonly List<string> AdditionalLinkerOptions = [];

    /// <summary>
    /// asynchronous task for setting up the linker.
    /// </summary>
    /// <returns>whether the setup was successful.</returns>
    public abstract Task<bool> Setup();

    /// <summary>
    /// Link the module.
    /// </summary>
    /// <returns>Whether the linking was successful.</returns>
    public abstract Task<bool> Link();

    public abstract string GetExecutablePath();

    public void SetModule(ModuleBase module)
    {
        CurrentModule = module;
    }
}