namespace ebuild.api;




/// <summary>
/// Interface for EBuild plugins
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// The name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Initializes the plugin.
    /// </summary>
    void Initialize();
}