using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

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

    public static async Task<string> GetMsvcToolRoot(string requirement = "Microsoft.VisualStudio.Component.VC.Tools.*")
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
}