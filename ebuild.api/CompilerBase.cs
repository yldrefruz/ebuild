// ReSharper disable UnusedMember.Global
// ReSharper disable PublicConstructorInAbstractClass
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local
// ReSharper disable NotAccessedField.Global

using System.Reflection;

namespace ebuild.api;

public abstract class CompilerBase
{
    /// <summary>
    /// The module we are currently working to compile
    /// </summary>
    protected ModuleBase? CurrentModule;

    public readonly List<string> AdditionalFlags = new();


    /// <summary>
    /// Checks if the compiler can be run in this state. 
    /// </summary>
    /// <param name="platform">The platform we are launching in.</param>
    /// <returns>whether the program can be run.</returns>
    public abstract bool IsAvailable(PlatformBase platform);

    /// <summary>
    /// Checks if the module has circular dependency.
    /// </summary>
    /// <returns>The list of modules with the circular dependency.</returns>
    public abstract List<ModuleBase> FindCircularDependencies();

    /// <summary>
    /// Generate the "thing" that we are asked for.
    /// </summary>
    /// <param name="type">the type of the "thing" we are asked for. For example can be <code>GenerateCompileCommands</code> for creating compile_commands.json</param>
    /// <returns>whether the generation was successful.</returns>
    public abstract Task<bool> Generate(string type);

    /// <summary>
    /// asynchronous task for setting up the compiler.
    /// </summary>
    /// <returns>whether the setup was successful.</returns>
    public abstract Task<bool> Setup();

    /// <summary>
    /// Compile the module.
    /// </summary>
    /// <returns>Whether the compilation was successful.</returns>
    public abstract Task<bool> Compile();

    public abstract string GetExecutablePath();

    public void SetModule(ModuleBase module)
    {
        CurrentModule = module;
    }

    public string GetName()
    {
        var compilerAttribute = GetType().GetCustomAttribute<CompilerAttribute>();
        if (compilerAttribute == null) throw new InvalidOperationException("get name requires CompilerAttribute");
        return compilerAttribute.GetName();
    }
}