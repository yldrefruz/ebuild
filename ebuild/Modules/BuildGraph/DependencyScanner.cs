using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ebuild.api;

namespace ebuild.Modules.BuildGraph;

/// <summary>
/// Scans source files for include dependencies
/// </summary>
public static class DependencyScanner
{
    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("DependencyScanner");
    private static readonly Regex IncludeRegex = new(@"^\s*#\s*include\s*[""<]([^""<>]+)[""<>]", RegexOptions.Compiled);

    /// <summary>
    /// Recursively scans a source file for all include dependencies
    /// </summary>
    /// <param name="sourceFile">The source file to scan</param>
    /// <param name="includePaths">List of include directories to search</param>
    /// <param name="module">The module context for determining platform-specific system includes</param>
    /// <param name="visited">Set of already visited files to prevent infinite recursion</param>
    /// <returns>List of all dependency file paths</returns>
    public static List<string> ScanDependencies(string sourceFile, List<string> includePaths, ModuleBase module, HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>();
        var dependencies = new List<string>();

        var normalizedPath = Path.GetFullPath(sourceFile);
        if (!visited.Add(normalizedPath))
            return dependencies; // Already visited

        if (!File.Exists(sourceFile))
            return dependencies;

        try
        {
            var lines = File.ReadAllLines(sourceFile);
            var sourceDir = Path.GetDirectoryName(sourceFile) ?? "";

            foreach (var line in lines)
            {
                var match = IncludeRegex.Match(line);
                if (!match.Success)
                    continue;

                var includePath = match.Groups[1].Value;
                var foundPath = FindIncludeFile(includePath, sourceDir, includePaths);

                if (!string.IsNullOrEmpty(foundPath) && !IsSystemInclude(foundPath, module))
                {
                    dependencies.Add(foundPath);

                    // Recursively scan the included file
                    var nestedDeps = ScanDependencies(foundPath, includePaths, module, visited);
                    dependencies.AddRange(nestedDeps);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to scan dependencies for {sourceFile}: {error}", sourceFile, ex.Message);
        }

        return dependencies;
    }

    private static string? FindIncludeFile(string includePath, string sourceDir, List<string> includePaths)
    {
        // Try relative to source file first
        var relativePath = Path.Combine(sourceDir, includePath);
        if (File.Exists(relativePath))
            return Path.GetFullPath(relativePath);

        // Try each include directory
        foreach (var includeDir in includePaths)
        {
            var fullPath = Path.Combine(includeDir, includePath);
            if (File.Exists(fullPath))
                return Path.GetFullPath(fullPath);
        }

        return null;
    }

    private static bool IsSystemInclude(string filePath, ModuleBase module)
    {
        var normalizedPath = Path.GetFullPath(filePath);

        // Get platform-specific include directories
        var platformIncludes = module.Context.Platform.GetPlatformIncludes(module);

        foreach (var systemDir in platformIncludes)
        {
            try
            {
                var normalizedSystemDir = Path.GetFullPath(systemDir);
                if (normalizedPath.StartsWith(normalizedSystemDir, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // Ignore paths that can't be normalized
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the latest modification time from a list of files
    /// </summary>
    public static DateTime GetLatestModificationTime(IEnumerable<string> files)
    {
        var latest = DateTime.MinValue;

        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file))
                {
                    var modTime = File.GetLastWriteTimeUtc(file);
                    if (modTime > latest)
                        latest = modTime;
                }
            }
            catch
            {
                // Ignore files that can't be accessed
            }
        }

        return latest;
    }
}