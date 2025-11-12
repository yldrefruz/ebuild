namespace ebuild.api;




/// <summary>
/// Interface for generating module files
/// </summary>
public interface IModuleFileGenerator
{
    /// <summary>
    /// The name of the module file generator.
    /// </summary>
    string Name { get; }
    /// <summary>
    /// Generates a module file at the specified path.
    /// </summary>
    /// <param name="moduleFilePath">The path where the module file should be generated.</param>
    /// <param name="force">If true, will overwrite an existing file.</param>
    /// <param name="templateOptions">Optional template options for generating the module file.</param>
    void Generate(string moduleFilePath, bool force, Dictionary<string, string>? templateOptions = null);

    /// <summary>
    /// Updates the C# solution to include references to dependencies of the module at the specified path.
    /// If the solution does not exist, it will be created.
    /// </summary>
    /// <param name="moduleFilePath">the path to the module file</param>
    void UpdateSolution(string moduleFilePath);
}