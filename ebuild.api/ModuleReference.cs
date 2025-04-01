using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class ModuleReference
{
    private string _path; // Absolute path to file
    private readonly string _output = "default"; // The output type
    private readonly Dictionary<string, string> _options = new();
    private readonly string _version = "latest";
    private static readonly Regex ReferenceRegex = ModuleReferenceStringRegex();

    private bool _resolved;

    public ModuleReference(string referenceString)
    {
        var match = ReferenceRegex.Match(referenceString);
        if (!match.Success)
            throw new ArgumentException(
                "Invalid reference string. Please see `module reference strings` section of the readme.");
        if (match.Groups["options"].Success)
        {
            foreach (var option in match.Groups["options"].Value.Split(';'))
            {
                var nameAndValue = option.Split('=');
                _options.Add(nameAndValue[0], nameAndValue[1]);
            }
        }

        _path = match.Groups["path"].Value;
        if (match.Groups["output"].Success)
        {
            _output = match.Groups["output"].Value;
        }

        if (match.Groups["version"].Success)
        {
            _version = match.Groups["version"].Value;
        }
    }


    public ModuleReference(string outputType, string path, string version, Dictionary<string, string> options)
    {
        _output = outputType;
        _path = path;
        _version = version;
        _options = options;
    }

    public static implicit operator ModuleReference(string file) => new(file);

    public string GetFilePath() => _path;
    public string GetOutput() => _output;
    public string GetVersion() => _version;
    public Dictionary<string, string> GetOptions() => _options;

    public void ResolveModulePath(ModuleBase resolverModule)
    {
        if (_resolved)
            return;
        _resolved = true;
        //0th resolve method. (If can be found directly, use)
        if (HasModule(_path, ref _path, out _)) return;
        //1st resolve method. (Resolve from dependency search paths)
        if (resolverModule.DependencySearchPaths.Any(dependencySearchPath =>
                HasModule(Path.Join(dependencySearchPath, _path), ref _path, out _)))
        {
            return;
        }

        //2nd resolve method. (--additional-dependency-paths option)
        if (resolverModule.Context.AdditionalDependencyPaths.Any(additionalDependencyPath =>
                HasModule(Path.Join(additionalDependencyPath, _path), ref _path, out _)))
        {
            return;
        }

        //3rd resolve method. (EBUILD_DEPENDENCY_PATH env var, seperated by;)
        if ((Environment.GetEnvironmentVariable("EBUILD_DEPENDENCY_PATH")
                 ?.Split(OperatingSystem.IsWindows() ? ";" : ":") ??
             Array.Empty<string>()).Any(ebuildDependencyPath =>
                HasModule(Path.Join(ebuildDependencyPath, _path), ref _path, out _)))
        {
            return;
        }

        //4th resolve method. (<module-dir>/.repo)
        if (HasModule(Path.Join(resolverModule.Context.ModuleFile.Directory!.FullName, ".repo", _path), ref _path,
                out _))
            return;

        //5th resolve method. (~/ebuild/.repo)
        if (HasModule(
                Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ebuild/.repo", _path),
                ref _path, out _))
            return;

        //6th resolve method. (%localappdata%/ebuild/.repo)
        if (HasModule(Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.Create),
                "ebuild/.repo", _path
            ), ref _path, out _))
        {
            return;
        }

        // 7th resolve method. ($PATH/<module>)
        if ((Environment.GetEnvironmentVariable("PATH")
                 ?.Split(OperatingSystem.IsWindows() ? ";" : ":") ??
             Array.Empty<string>()).Any(p => HasModule(Path.Join(p, _path), ref _path, out _)))
        {
            return;
        }
    }

    private static bool HasModule(string path, ref string found, out string name)
    {
        if (File.Exists(path))
        {
            name = Path.GetFileNameWithoutExtension(path);
            found = path;
            return true;
        }

        if (File.Exists(Path.Join(path, "index.ebuild.cs")))
        {
            name = new DirectoryInfo(path).Name;
            found = Path.Join(path, "index.ebuild.cs");
            // index.ebuild.cs is the default file name or the most preferred one. for packages from internet.
            return true;
        }

        if (File.Exists(Path.Join(path, new DirectoryInfo(path).Name + ".ebuild.cs")))
        {
            name = new DirectoryInfo(path).Name;
            found = Path.Join(path, new DirectoryInfo(path).Name + ".ebuild.cs");
            // package <name>.ebuild.cs is the second most preferred one.
            return true;
        }

        if (File.Exists(Path.Join(path, "ebuild.cs")))
        {
            name = new DirectoryInfo(path).Name;
            found = Path.Join(path, "ebuild.cs"); // ebuild.cs is the third most preferred one.
            return true;
        }

        name = string.Empty;
        return false;
    }

    [GeneratedRegex(
            @"^(?:(?<output>\w+):)?(?<path>(?:[^/\\]*[/\\])*(?:[^@?!]*))(?:@(?<version>\w+))?(?:\?(?<options>(?:[\w-_]+=[\w-_]+;?)*))?$")]
    private static partial Regex ModuleReferenceStringRegex();
}