using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ebuild;

public static class MSVCUtils
{
    public const string VsWhereUrl = "https://github.com/microsoft/vswhere/releases/download/3.1.7/vswhere.exe";
    private const string VsWhereHash = "C54F3B7C9164EA9A0DB8641E81ECDDA80C2664EF5A47C4191406F848CC07C662";

    public static string GetVsWhereDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Join(localAppData, "ebuild", "msvc", "vswhere");
    }

    public static async Task<string> GetMsvcToolRoot(string requirement = "Microsoft.VisualStudio.Component.VC.*")
    {
        var toolRoot = Config.Get().MsvcPath ?? string.Empty;
        if (string.IsNullOrEmpty(toolRoot))
        {
            var vsWhereExecutable = Path.Join(GetVsWhereDirectory(), "vswhere.exe");
            var args = $"-latest -products * -requires {requirement} -property installationPath";
            var vsWhereProcess = new Process();
            var processStartInfo = new ProcessStartInfo
            {
                Arguments = args,
                FileName = vsWhereExecutable,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            vsWhereProcess.StartInfo = processStartInfo;
            vsWhereProcess.Start();
            toolRoot = await vsWhereProcess.StandardOutput.ReadToEndAsync();
            await vsWhereProcess.WaitForExitAsync();
        }

        return toolRoot.Trim();
    }

    public static bool VswhereExists()
    {
        var vsWhereExec = Path.Join(GetVsWhereDirectory(), "vswhere.exe");
        if (!File.Exists(vsWhereExec))
            return false;
        using var shaHasher = SHA256.Create();
        using var fs = File.Open(vsWhereExec, FileMode.Open);
        var hash = shaHasher.ComputeHash(fs);
        var stringBuilder = new StringBuilder();

        foreach (var b in hash)
            stringBuilder.AppendFormat("{0:X2}", b);
        var hashString = stringBuilder.ToString();
        return hashString == VsWhereHash;
    }

    public static bool DownloadVsWhere()
    {
        HttpClient client = new HttpClient();
        byte[] vswhereBytes;
        try
        {
            var job = client.GetByteArrayAsync(VsWhereUrl);
            job.Wait();
            if (!job.IsCompletedSuccessfully)
                return false;
            vswhereBytes = job.Result;
        }
        catch (Exception)
        {
            return false;
        }

        var pathSegments = VsWhereUrl.Split("/");
        var version = pathSegments[pathSegments.Length - 1 - 1];
        var vswhereDirectory = GetVsWhereDirectory();
        Directory.CreateDirectory(vswhereDirectory);
        var versionFile = File.Create(Path.Join(vswhereDirectory, "VERSION"));
        TextWriter writer = new StreamWriter(versionFile, Encoding.UTF8);
        writer.Write(version);
        writer.Close();
        writer.Dispose();
        versionFile.Close();
        var vswhereFile = File.Create(Path.Join(vswhereDirectory, "vswhere.exe"));
        vswhereFile.Write(vswhereBytes);
        vswhereFile.Close();
        vswhereFile.Dispose();
        return true;
    }

    /// <summary>
    /// Finds and returns the MSVC version to use.
    /// First checks the configured version, then discovers available versions if needed.
    /// </summary>
    /// <param name="toolRoot">The MSVC tool root directory</param>
    /// <param name="logger">Logger instance for logging discovery process</param>
    /// <returns>The MSVC version string to use, or null if not found</returns>
    public static async Task<string?> FindMsvcVersion(string toolRoot, ILogger logger)
    {
        var version = Config.Get().MsvcVersion ?? string.Empty;
        version = version.Trim();

        if (!string.IsNullOrEmpty(version) && File.Exists(Path.Join(toolRoot, "VC", "Tools", "MSVC", version)))
        {
            return version;
        }

        if (!string.IsNullOrEmpty(version))
        {
            logger.LogInformation("(Config) => Msvc Version: {version} is not found, trying to find a valid version.",
                version);
        }
        else
        {
            logger.LogInformation("(Config) => Msvc Version: <Empty> is not found, trying to find a valid version.");
        }

        // Discover available versions
        Dictionary<Version, string> versionDict = [];
        var versionFilesPath = Path.Join(toolRoot, "VC", "Auxiliary", "Build");

        if (!Directory.Exists(versionFilesPath))
        {
            logger.LogError("MSVC version files directory not found: {path}", versionFilesPath);
            return null;
        }

        foreach (var file in Directory.GetFiles(versionFilesPath, "Microsoft.VCToolsVersion.*default.txt"))
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                if (Version.TryParse(content, out var foundVer))
                {
                    versionDict.Add(foundVer, content);
                    using (logger.BeginScope("Version Discovery"))
                    {
                        logger.LogInformation("Found version: {content}", content);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to read version file: {file}", file);
            }
        }

        var latestVer = versionDict.Keys.ToList().OrderDescending().FirstOrDefault();
        return latestVer != null ? versionDict[latestVer].Trim() : null;
    }

    /// <summary>
    /// Sets up MSVC paths using the discovered version.
    /// </summary>
    /// <param name="toolRoot">The MSVC tool root directory</param>
    /// <param name="version">The MSVC version to use</param>
    /// <returns>A tuple containing (msvcToolRoot, msvcCompilerRoot)</returns>
    public static (string msvcToolRoot, string msvcCompilerRoot) SetupMsvcPaths(string toolRoot, string version)
    {
        var msvcToolRoot = Path.Join(toolRoot, "VC", "Tools", "MSVC", version);
        var host = Environment.Is64BitOperatingSystem ? "Hostx64" : "Hostx86";
        var msvcCompilerRoot = Path.Join(msvcToolRoot, "bin", host);

        return (msvcToolRoot, msvcCompilerRoot);
    }

    public class WindowsKitInformation(string version, string kitRoot)
    {
        public readonly string Version = version;
        public readonly string IncludePath = Path.Join(kitRoot, "Include", version);
        public readonly string LibPath = Path.Join(kitRoot, "Lib", version);

        public readonly string KitRoot = kitRoot;
    }

    private static WindowsKitInformation[]? cachedInformation = null;


    public static WindowsKitInformation? GetWindowsKit(string? version)
    {
        var kits = GetWindowsKits();
        if (version == null)
        {
            if (kits.Length == 0)
                return null;
            return kits.OrderByDescending(k => Version.Parse(k.Version)).FirstOrDefault();
        }
        foreach (var kit in kits)
        {
            if (kit.Version == version)
                return kit;
        }
        return null;
    }

    public static WindowsKitInformation[] GetWindowsKits()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return [];
        if (cachedInformation == null)
            cachedInformation = DiscoverWindowsKits();
        return cachedInformation;

    }


    private static WindowsKitInformation[] DiscoverWindowsKits()
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return [];
        List<WindowsKitInformation> returnArr = [];
        const string kitsRegPath = @"SOFTWARE\Microsoft\Windows Kits\Installed Roots";
        try
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
            using var kitsKey = baseKey.OpenSubKey(kitsRegPath);
            if (kitsKey != null)
            {
                var kitRoot10 = kitsKey.GetValue("KitsRoot10") as string;
                var kitRoot81 = kitsKey.GetValue("KitsRoot81") as string;
                foreach (var subkey in kitsKey.GetSubKeyNames())
                {
                    if (subkey.StartsWith("10."))
                    {
                        if (kitRoot10 != null)
                            returnArr.Add(new WindowsKitInformation(subkey, kitRoot10));
                    }
                    else if (subkey.StartsWith("8.1"))
                    {
                        if (kitRoot81 != null)
                            returnArr.Add(new WindowsKitInformation(subkey, kitRoot81));
                    }
                }
            }

        }
        catch
        {
            // Ignore registry access errors
        }
        return [.. returnArr];
    }
}