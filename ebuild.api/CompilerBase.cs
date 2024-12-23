// ReSharper disable UnusedMember.Global
// ReSharper disable PublicConstructorInAbstractClass
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local
// ReSharper disable NotAccessedField.Global

namespace ebuild.api;

public abstract class CompilerBase
{
    public CompilerBase(ModuleBase module, ModuleContext moduleContext)
    {
        CurrentModule = module;
        CurrentModuleContext = moduleContext;
    }

    /// <summary>
    /// The module we are currently working to compile
    /// </summary>
    protected ModuleBase CurrentModule;

    /// <summary>
    /// The module context applied to the module.
    /// This helps us have the information about where we will output the files.
    /// </summary>
    protected ModuleContext CurrentModuleContext;

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
    public abstract List<ModuleBase> HasCircularDependency();

    /// <summary>
    /// Generate the "thing" that we are asked for.
    /// </summary>
    /// <param name="type">the type of the "thing" we are asked for. For example can be <code>GenerateCompileCommands</code> for creating compile_commands.json</param>
    /// <returns>whether the generation was successful.</returns>
    public abstract bool Generate(string type);

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
}