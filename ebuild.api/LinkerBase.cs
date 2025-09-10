using System.Reflection;

namespace ebuild.api;

public abstract class LinkerBase
{
    /// <summary>
    /// The module we are currently working to link
    /// </summary>
    protected ModuleBase? CurrentModule;

    public readonly List<string> AdditionalLinkerOptions = [];

    /// <summary>
    /// Checks if the linker can be run in this state.
    /// </summary>
    /// <param name="platform">The platform we are launching in.</param>
    /// <returns>whether the linker can be run.</returns>
    public abstract bool IsAvailable(PlatformBase platform);

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

    public string GetName()
    {
        var linkerAttribute = GetType().GetCustomAttribute<LinkerAttribute>();
        if (linkerAttribute == null) throw new InvalidOperationException("get name requires LinkerAttribute");
        return linkerAttribute.GetName();
    }
}