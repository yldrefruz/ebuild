using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;

namespace ebuild;

/// <summary>
/// Manages checking for and applying updates to ebuild
/// </summary>
public class UpdateManager
{
    private readonly ILogger _logger;
    private const string GithubApiUrl = "https://api.github.com/repos/yldrefruz/ebuild/releases/latest";
    
    public UpdateManager()
    {
        _logger = EBuild.LoggerFactory.CreateLogger<UpdateManager>();
    }

    /// <summary>
    /// Gets the current version of ebuild
    /// </summary>
    public static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        return version;
    }

    /// <summary>
    /// Gets the current informational version string (includes pre-release info)
    /// </summary>
    public static string GetCurrentVersionString()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return infoVersionAttr?.InformationalVersion ?? GetCurrentVersion().ToString();
    }

    /// <summary>
    /// Checks if an update is available
    /// </summary>
    /// <returns>Tuple of (isAvailable, latestVersion, downloadUrl, releaseNotes)</returns>
    public async Task<(bool isAvailable, string? latestVersion, string? downloadUrl, string? releaseNotes, string? sha256)> CheckForUpdateAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ebuild-updater");
            
            // Add GitHub token if available (for higher rate limits)
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(githubToken))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubToken}");
            }
            
            _logger.LogInformation("Checking for updates...");
            
            var response = await client.GetAsync(GithubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning("GitHub API rate limit exceeded. Set GITHUB_TOKEN environment variable for higher limits.");
                }
                else
                {
                    _logger.LogWarning($"GitHub API request failed with status code: {response.StatusCode}");
                }
                return (false, null, null, null, null);
            }
            
            var responseBody = await response.Content.ReadAsStringAsync();
            var release = JsonNode.Parse(responseBody);
            
            if (release == null)
            {
                _logger.LogWarning("Failed to parse release information");
                return (false, null, null, null, null);
            }

            var tagName = release["tag_name"]?.ToString();
            var body = release["body"]?.ToString();
            
            if (string.IsNullOrEmpty(tagName))
            {
                _logger.LogWarning("Release tag name not found");
                return (false, null, null, null, null);
            }

            // Remove 'v' prefix if present
            var versionString = tagName.TrimStart('v');
            
            // Try to parse as semantic version
            if (!Version.TryParse(versionString, out var latestVersion))
            {
                _logger.LogWarning($"Could not parse version from tag: {tagName}");
                return (false, null, null, null, null);
            }

            var currentVersion = GetCurrentVersion();
            var isNewer = latestVersion > currentVersion;
            
            if (!isNewer)
            {
                _logger.LogInformation($"Current version {currentVersion} is up to date");
                return (false, versionString, null, body, null);
            }

            // Determine platform-specific asset
            var assetName = GetPlatformAssetName();
            var assets = release["assets"]?.AsArray();
            
            if (assets == null)
            {
                _logger.LogWarning("No assets found in release");
                return (true, versionString, null, body, null);
            }

            foreach (var asset in assets)
            {
                if (asset == null) continue;
                var name = asset["name"]?.ToString();
                if (name == assetName)
                {
                    var downloadUrl = asset["browser_download_url"]?.ToString();
                    
                    // Extract SHA256 from release notes if available
                    string? sha256 = null;
                    if (!string.IsNullOrEmpty(body))
                    {
                        sha256 = ExtractSha256FromReleaseNotes(body, assetName);
                    }
                    
                    _logger.LogInformation($"Update available: {versionString}");
                    return (true, versionString, downloadUrl, body, sha256);
                }
            }
            
            _logger.LogWarning($"No asset found for platform: {assetName}");
            return (true, versionString, null, body, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return (false, null, null, null, null);
        }
    }

    /// <summary>
    /// Downloads and applies an update
    /// </summary>
    public async Task<bool> ApplyUpdateAsync(string downloadUrl, string? expectedSha256 = null)
    {
        try
        {
            _logger.LogInformation($"Downloading update from {downloadUrl}");
            
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ebuild-updater");
            
            var zipData = await client.GetByteArrayAsync(downloadUrl);
            
            // Verify checksum if provided
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                var actualSha256 = ComputeSha256(zipData);
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError($"SHA256 mismatch! Expected: {expectedSha256}, Got: {actualSha256}");
                    return false;
                }
                _logger.LogInformation("SHA256 checksum verified successfully");
            }

            // Extract to temporary directory
            var tempDir = Path.Combine(Path.GetTempPath(), $"ebuild-update-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                _logger.LogInformation($"Extracting update to {tempDir}");
                ExtractZip(zipData, tempDir);
                
                // Find the ebuild executable in the extracted files
                var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ebuild.exe" : "ebuild";
                var newExecutable = FindFile(tempDir, executableName);
                
                if (string.IsNullOrEmpty(newExecutable))
                {
                    _logger.LogError($"Could not find {executableName} in the update package");
                    return false;
                }

                // Get current executable path
                var currentExecutable = Environment.ProcessPath;
                if (string.IsNullOrEmpty(currentExecutable))
                {
                    _logger.LogError("Could not determine current executable path");
                    return false;
                }

                var currentDir = Path.GetDirectoryName(currentExecutable);
                if (string.IsNullOrEmpty(currentDir))
                {
                    _logger.LogError("Could not determine current executable directory");
                    return false;
                }

                _logger.LogInformation("Replacing current installation...");
                
                // Create backup
                var backupPath = currentExecutable + ".backup";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Copy(currentExecutable, backupPath, true);
                
                // Copy all files from extracted directory to current directory
                CopyDirectory(tempDir, currentDir, true);
                
                _logger.LogInformation("Update applied successfully!");
                _logger.LogInformation("Please restart ebuild to use the new version.");
                
                // Clean up backup
                if (File.Exists(backupPath))
                {
                    try
                    {
                        File.Delete(backupPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                
                return true;
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying update");
            return false;
        }
    }

    private static string GetPlatformAssetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "ebuild-windows.zip";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "ebuild-linux.zip";
        }
        else
        {
            throw new PlatformNotSupportedException($"Platform {RuntimeInformation.OSDescription} is not supported for automatic updates");
        }
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static string? ExtractSha256FromReleaseNotes(string releaseNotes, string assetName)
    {
        // Look for patterns like: `assetName`: sha256:hash or assetName sha256:hash
        var lines = releaseNotes.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains(assetName) && line.Contains("sha256:"))
            {
                var sha256Index = line.IndexOf("sha256:");
                if (sha256Index >= 0)
                {
                    var hashStart = sha256Index + 7; // length of "sha256:"
                    var remaining = line.Substring(hashStart).Trim();
                    
                    // Extract the hash (64 hex characters)
                    var hash = "";
                    foreach (var c in remaining)
                    {
                        if (char.IsLetterOrDigit(c))
                        {
                            hash += c;
                        }
                        else if (hash.Length > 0)
                        {
                            break;
                        }
                    }
                    
                    if (hash.Length == 64)
                    {
                        return hash;
                    }
                }
            }
        }
        return null;
    }

    private static void ExtractZip(byte[] zipData, string destinationPath)
    {
        using var memoryStream = new MemoryStream(zipData);
        using var zipInputStream = new ZipInputStream(memoryStream);
        
        ZipEntry? entry;
        while ((entry = zipInputStream.GetNextEntry()) != null)
        {
            var entryPath = Path.Combine(destinationPath, entry.Name);
            var directoryName = Path.GetDirectoryName(entryPath);
            
            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            if (!entry.IsDirectory)
            {
                using var fileStream = File.Create(entryPath);
                zipInputStream.CopyTo(fileStream);
                
                // Set executable permission on Unix-like systems
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Make executable
                    if (entry.Name.EndsWith("ebuild") || entry.Name.Contains("/ebuild"))
                    {
                        try
                        {
                            File.SetUnixFileMode(entryPath, 
                                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                        }
                        catch
                        {
                            // Ignore permission errors
                        }
                    }
                }
            }
        }
    }

    private static string? FindFile(string directory, string fileName)
    {
        foreach (var file in Directory.GetFiles(directory, fileName, SearchOption.AllDirectories))
        {
            return file;
        }
        return null;
    }

    private static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
    {
        var dir = new DirectoryInfo(sourceDir);
        
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        // Copy all files
        foreach (var file in dir.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file.FullName);
            var destPath = Path.Combine(destDir, relativePath);
            
            var destFileDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destFileDir))
            {
                Directory.CreateDirectory(destFileDir);
            }
            
            file.CopyTo(destPath, overwrite);
            
            // Preserve executable permissions on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var mode = File.GetUnixFileMode(file.FullName);
                    File.SetUnixFileMode(destPath, mode);
                }
                catch
                {
                    // Ignore permission errors
                }
            }
        }
    }
}
