using System.Xml.Serialization;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild;

[Serializable]
public class Config
{
    protected static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("Config");

    /// <summary>
    /// The preferred compiler to use. If not found will fallback to platform preferred compiler.
    /// This can be a fatal error, configure with <see cref="IsPreferredCompilerNotFoundFatal"/>
    /// </summary>
    public string PreferredCompilerName = string.Empty;

    /// <summary>
    /// If the preferred compiler is not found should ebuild exit with a fatal error.
    /// <seealso cref="PreferredCompilerName"/>
    /// </summary>
    public bool IsPreferredCompilerNotFoundFatal = true;

    /// <summary>
    /// The msvc version to use. Empty or "latest" for using the latest version available.
    ///
    /// Specified version should exist. If the version is not available will exit with a fatal error
    /// when msvc should be used.
    /// </summary>
    public string? MsvcVersion;

    /// <summary>
    /// The path to msvc. It should be the path under the tools directory. Can be empty to find with VSWhere.exe .
    /// 
    /// <example>C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC</example>
    /// </summary>
    public string? MsvcPath;

    /// <summary>
    /// Default paths to find modules. will look for the directory itself and one depth children.
    /// <example>["${ebuild_dir}/modules/","${ebuild_dir}/net_modules/"]</example>
    /// macros: ${ebuild_dir}: the directory ebuild.exe/ebuild.dll is in.
    /// </summary>
    public List<string>? DefaultModulePaths;

    /// <summary>
    /// The trusted github repositories to fetch the module files. The repository needs an ebuild.module_repository file
    /// which will define the repositories available and the corresponding files.
    ///
    /// It is advised that the git repository doesn't actually contain the modules to be built but a module for
    /// downloading the project at run-time.
    /// </summary>
    public List<string>? TrustedGithubRepositories;

    /// <summary>
    /// Setup the defaults.
    /// </summary>
    private void Setup()
    {
        PreferredCompilerName = PlatformRegistry.GetHostPlatform().GetDefaultCompilerName() ?? string.Empty;
    }

    private static string LocalFile =>
        Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create), "ebuild", "config", "main.xml");

    private static Config? _instance;

    public static Config Get()
    {
        if (!File.Exists(LocalFile))
        {
            Logger.LogInformation("Creating config file at {fileName}", LocalFile);
            _instance = new Config();
            _instance.Setup();
            _instance.Save();
        }
        else if (_instance == null)
        {
            _instance = Load();
        }

        return _instance!;
    }

    private static XmlSerializer _serializer = new XmlSerializer(typeof(Config));

    public void Save()
    {
        Directory.CreateDirectory(Directory.GetParent(LocalFile)!.FullName);
        using var fs = new FileStream(LocalFile, FileMode.Create);
        _serializer.Serialize(fs, this);
        Logger.LogInformation("Saving config file: {fileName}", LocalFile);
    }

    public static Config? Load()
    {
        using var fs = new FileStream(LocalFile, FileMode.OpenOrCreate);
        Logger.LogInformation("Loading config file: {fileName}", LocalFile);
        return (Config?)_serializer.Deserialize(fs);
    }
}