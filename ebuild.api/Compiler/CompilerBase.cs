using ebuild.api.Linker;

namespace ebuild.api.Compiler;

public abstract class CompilerBase(ModuleBase module, IModuleInstancingParams instancingParams)
{
    /// <summary>
    /// The module we are currently working to compile
    /// </summary>
    protected ModuleBase CurrentModule = module;
    public IModuleInstancingParams InstancingParams = instancingParams;

    /// <summary>
    /// The linker to use for linking operations
    /// </summary>
    protected LinkerBase? Linker;

    public readonly List<string> AdditionalCompilerOptions = [];
    public readonly List<string> AdditionalLinkerOptions = [];

    public bool CleanCompilation = false;
    public int? ProcessCount = null;

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
    /// <param name="data">the additional data to use</param>
    /// <returns>whether the generation was successful.</returns>
    public abstract Task<bool> Generate(string type, object? data = null);

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
        Linker?.SetModule(module);
    }

    public void SetLinker(LinkerBase linker)
    {
        Linker = linker;
        if (CurrentModule != null)
        {
            Linker.SetModule(CurrentModule);
        }
        // Copy additional linker options to the linker
        Linker.AdditionalLinkerOptions.AddRange(AdditionalLinkerOptions);
    }
}