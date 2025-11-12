using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ebuild.api
{
    /// <summary>
    /// Represents a reference to another module. Module references are parsed from a
    /// compact string form (see constructor) and may be resolved to concrete file paths
    /// using the <see cref="ResolveModulePath"/> method which consults several locations
    /// (relative paths, dependency search paths, environment variables, repository folders, PATH).
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public partial class ModuleReference
    {
        /// <summary>Absolute or relative path portion of the reference (may be resolved later).</summary>
        private string _path; // Absolute path to file

        /// <summary>The requested output transformer id (defaults to "default").</summary>
        private readonly string _output = "default"; // The output type

        /// <summary>Option map parsed from reference strings (key &amp; value pairs).</summary>
        private readonly Dictionary<string, string> _options = [];

        /// <summary>Requested version token (defaults to "latest").</summary>
        private readonly string _version = "latest";

        /// <summary>Compiled regex used to parse module reference strings.</summary>
        private static readonly Regex ReferenceRegex = ModuleReferenceStringRegex();

        private bool _resolved;

        /// <summary>
        /// Parse a module reference from a compact reference string. The accepted format is:
        /// [output:]path[@version][?key=value;key2=value2]
        /// If parsing fails the constructor throws an <see cref="ArgumentException"/>.
        /// </summary>
        /// <param name="referenceString">The compact module reference string to parse.</param>
        /// <exception cref="ArgumentException">Thrown when the input string doesn't match the expected format.</exception>
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


        /// <summary>
        /// Construct a module reference from explicit components.
        /// </summary>
        /// <param name="outputType">Output transformer id.</param>
        /// <param name="path">Module path or identifier.</param>
        /// <param name="version">Version token.</param>
        /// <param name="options">Options map.</param>
        public ModuleReference(string outputType, string path, string version, Dictionary<string, string> options)
        {
            _output = outputType;
            _path = path;
            _version = version;
            _options = options;
        }

        /// <summary>
        /// Implicit conversion from string to <see cref="ModuleReference"/>, delegating to the parsing constructor.
        /// </summary>
        /// <param name="file">Compact module reference string.</param>
        public static implicit operator ModuleReference(string file) => new(file);

        /// <summary>Return the (possibly unresolved) file path portion of this reference.</summary>
        public string GetFilePath() => _path;

        /// <summary>Return the requested output transformer id for this reference.</summary>
        public string GetOutput() => _output;

        /// <summary>Return the requested version token for this reference.</summary>
        public string GetVersion() => _version;

        /// <summary>Return the option map parsed from the reference string.</summary>
        public Dictionary<string, string> GetOptions() => _options;

        /// <summary>
        /// Resolve the module path to a concrete file on disk. This method attempts multiple
        /// strategies in order (direct path, dependency search paths, additional dependency paths,
        /// EBUILD_DEPENDENCY_PATH environment variable, module-local .repo, user repo locations, PATH).
        /// Once resolved the internal path is updated and subsequent calls are no-ops.
        /// </summary>
        /// <param name="resolverModule">Optional module used as a relative resolution base (provides DependencySearchPaths and ModuleDirectory).</param>
        public void ResolveModulePath(ModuleBase? resolverModule)
        {
            if (_resolved)
                return;
            _resolved = true;
            //0th resolve method. (If can be found directly, use)
            if (HasModule(_path, ref _path, out _, resolverModule)) return;
            //1st resolve method. (Resolve from dependency search paths)
            if (resolverModule != null && resolverModule.DependencySearchPaths.Any(dependencySearchPath =>
                    HasModule(Path.Join(dependencySearchPath, _path), ref _path, out _, resolverModule)))
            {
                return;
            }

            //2nd resolve method. (--additional-dependency-paths option)
            if (resolverModule != null && resolverModule.Context.AdditionalDependencyPaths.Any(additionalDependencyPath =>
                    HasModule(Path.Join(additionalDependencyPath, _path), ref _path, out _, resolverModule)))
            {
                return;
            }

            //3rd resolve method. (EBUILD_DEPENDENCY_PATH env var, seperated by;)
            if ((Environment.GetEnvironmentVariable("EBUILD_DEPENDENCY_PATH")
                     ?.Split(OperatingSystem.IsWindows() ? ";" : ":") ??
                 Array.Empty<string>()).Any(ebuildDependencyPath =>
                    HasModule(Path.Join(ebuildDependencyPath, _path), ref _path, out _, resolverModule)))
            {
                return;
            }

            //4th resolve method. (<module-dir>/.repo)
            if (resolverModule != null && HasModule(Path.Join(resolverModule.Context.ModuleFile.Directory!.FullName, ".repo", _path), ref _path,
                    out _, resolverModule))
                return;

            //5th resolve method. (~/ebuild/.repo)
            if (HasModule(
                    Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ebuild/.repo", _path),
                    ref _path, out _, resolverModule))
                return;

            //6th resolve method. (%localappdata%/ebuild/.repo)
            if (HasModule(Path.Join(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                        Environment.SpecialFolderOption.Create),
                    "ebuild/.repo", _path
                ), ref _path, out _, resolverModule))
            {
                return;
            }

            // 7th resolve method. ($PATH/<module>)
            if ((Environment.GetEnvironmentVariable("PATH")
                     ?.Split(OperatingSystem.IsWindows() ? ";" : ":") ??
                 Array.Empty<string>()).Any(p => HasModule(Path.Join(p, _path), ref _path, out _, resolverModule)))
            {
                return;
            }
        }

        /// <summary>
        /// Test whether the provided candidate path refers to an existing module file. The
        /// method considers several canonical file name patterns (plain file, index.ebuild.cs,
        /// &lt;dirname&gt;.ebuild.cs, ebuild.cs) and returns the resolved absolute path in <paramref name="found"/>.
        /// </summary>
        /// <param name="path">Candidate path to test.</param>
        /// <param name="found">Out parameter updated with the resolved path when true is returned.</param>
        /// <param name="name">Out parameter that receives the short module name (file base name or directory name).</param>
        /// <param name="relativeTo">Optional module used to provide a relative base directory for resolution.</param>
        /// <returns><c>true</c> when a module file was found; otherwise <c>false</c>.</returns>
        private static bool HasModule(string path, ref string found, out string name, ModuleBase? relativeTo = null)
        {
            if (File.Exists(Path.GetFullPath(path, relativeTo?.Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory())))
            {
                name = Path.GetFileNameWithoutExtension(path);
                found = Path.GetFullPath(path, relativeTo?.Context.ModuleDirectory?.FullName ?? Directory.GetCurrentDirectory());
                return true;
            }
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
                @"^(?:(?<output>\w+):)?(?<path>(?:[^\/\\]*[\/\\])*(?:[^@?!]*))(?:@(?<version>\w+))?(?:\?(?<options>(?:[\w\-_]+=[\w\-_]+;?)*))?$")]
        private static partial Regex ModuleReferenceStringRegex();



        /// <summary>
        /// Reconstruct the compact string representation of the reference (matching the input format):
        /// [output:]path[@version][?key=value;...]
        /// </summary>
        public override string ToString()
        {
            var optionsString = string.Join(";", _options.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"{(_output != "default" ? _output + ":" : string.Empty)}{_path}{(_version != "latest" ? "@" + _version : string.Empty)}{(!string.IsNullOrEmpty(optionsString) ? "?" + optionsString : string.Empty)}";
        }
    }
}