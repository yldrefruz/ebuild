using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ebuild.Compilers;

public class MsvcCompiler : Compiler
{
    private readonly string _msvcCompilerRoot;
    private readonly string _msvcToolRoot;

    private static string GetVsWhereDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Join(localAppData, "ebuild", "compilers", "msvc", "vswhere");
    }

    private bool VswhereExists()
    {
        var vsWhereHash = "C54F3B7C9164EA9A0DB8641E81ECDDA80C2664EF5A47C4191406F848CC07C662";
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
        return hashString == vsWhereHash;
    }

    private const string VsWhereUrl = "https://github.com/microsoft/vswhere/releases/download/3.1.7/vswhere.exe";

    bool DownloadVsWhere()
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

    public MsvcCompiler()
    {
        if (!VswhereExists())
        {
            if (!DownloadVsWhere())
            {
                throw new Exception(
                    $"Can't download vswhere from {VsWhereUrl}. Please check your internet connection.");
            }
        }

        var vsWhereExecutable = Path.Join(GetVsWhereDirectory(), "vswhere.exe");
        var args =
            "-latest -products * -requires \"Microsoft.VisualStudio.Component.VC.Tools.x86.x64\" -property installationPath";
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
        var vsWhereOutput = vsWhereProcess.StandardOutput.ReadToEnd();
        vsWhereProcess.WaitForExit();
        vsWhereOutput = vsWhereOutput.Trim();
        var version = File.ReadAllText(Path.Join(vsWhereOutput, "VC", "Auxiliary", "Build",
            "Microsoft.VCToolsVersion.default.txt"));
        version = version.Trim();
        _msvcToolRoot = Path.Join(vsWhereOutput, "VC", "Tools", "MSVC", version);
        var host = "Hostx86";
        if (Environment.Is64BitOperatingSystem)
        {
            host = "Hostx64";
        }

        _msvcCompilerRoot = Path.Join(_msvcToolRoot, "bin", host);
    }


    public override string GetName()
    {
        return "MSVC";
    }

    public override string GetExecutablePath()
    {
        var msvcCompilerBin = GetMsvcCompilerBin();
        return Path.Join(msvcCompilerBin, "cl.exe");
    }

    private string GetMsvcCompilerBin()
    {
        var targetArch = "x86";
        if (GetCurrentTarget() != null && GetCurrentTarget()!.Architecture == Architecture.X64)
            targetArch = "x64";
        var msvcCompilerBin = Path.Join(_msvcCompilerRoot, targetArch);
        return msvcCompilerBin;
    }

    private static string GetShorterPath(string path)
    {
        var fp = Path.GetFullPath(path).Replace("\\", @"\\");
        //We are in binary, so we should resolve the path from the binary folder.
        var rp = Path.GetRelativePath(Path.Join(Directory.GetCurrentDirectory(), "Binaries"), path)
            .Replace("\\", @"\\");
        return fp.Length > rp.Length ? rp : fp;
    }

    public override void Compile(ModuleContext moduleContext)
    {
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null) return;
        Console.WriteLine("Starting the Compiler section.");
        var commandFileContent = "/nologo /c ";
        foreach (var definition in currentTarget.Definitions)
        {
            commandFileContent += $"/D\"{definition}\"";
            commandFileContent += " ";
        }

        if (currentTarget.UseDefaultIncludes)
        {
            currentTarget.Includes.AddRange([
                Path.Join(_msvcToolRoot, "include"),
                //TODO: programatically find this.
                "C:\\Program Files (x86)\\Windows Kits\\10\\Include\\10.0.22621.0\\ucrt"
            ]);
        }

        var binaryDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Directory.GetParent(binaryDir)!.FullName);
        foreach (var include in currentTarget.Includes)
        {
            var toInclude = $"/I\"{GetShorterPath(include)}\"";
            commandFileContent += toInclude;
            commandFileContent += " ";
        }

        foreach (var source in currentTarget.SourceFiles)
        {
            commandFileContent += '"' + GetShorterPath(source) + '"';
            commandFileContent += " ";
        }


        var commandFilePath = Path.GetTempFileName();
        using (var commandFile = File.OpenWrite(commandFilePath))
        {
            using var writer = new StreamWriter(commandFile);
            writer.Write(commandFileContent);
            writer.Flush();
            commandFile.Flush();
        }

        Directory.SetCurrentDirectory(binaryDir);
        var startInfo = new ProcessStartInfo()
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Arguments = $"@\"{commandFilePath}\"",
            FileName = GetExecutablePath(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        var proc = Process.Start(startInfo);
        if (proc == null)
        {
            Console.WriteLine("Can't start cl.exe");
            return;
        }

        proc.Start();
        Console.WriteLine(proc.StandardError.ReadToEnd());
        Console.WriteLine(proc.StandardOutput.ReadToEnd());
        proc.WaitForExit();
        if (File.Exists(commandFilePath))
            File.Delete(commandFilePath);
        if (proc.ExitCode != 0)
        {
            Console.WriteLine("Compilation Failed, {0}", proc.ExitCode);
            Console.WriteLine("Command File Content was:\n{0}", commandFileContent);
            Console.Out.Flush();
            return;
        }

        Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName);
        Console.WriteLine("End of compiling.");
        if (currentTarget.Type == ModuleType.StaticLibrary)
        {
            Console.WriteLine("Starting LIB.EXE");
            var libExe = Path.Join(GetMsvcCompilerBin(), "lib.exe");
            var files = Directory.GetFiles(Path.Join(Directory.GetCurrentDirectory(), "Binaries"));
            files = files.Where(s => s.EndsWith(".obj")).ToArray();
            files = files.Select(GetShorterPath).ToArray();
            Directory.CreateDirectory(Path.Join(binaryDir, "lib"));
            var libCommandFileContent =
                $"/nologo /verbose /OUT:\"{Path.Join(binaryDir, "lib", currentTarget.Name + ".lib")}\" ";

            foreach (var libPath in currentTarget.LibrarySearchPaths)
            {
                libCommandFileContent += $"/LIBPATH:\"{Path.GetFullPath(libPath)}\"";
                libCommandFileContent += " ";
            }

            foreach (var file in files)
            {
                libCommandFileContent += $"\"{file}\"";
                libCommandFileContent += " ";
            }

            foreach (var library in currentTarget.Libraries)
            {
                var mutableLibrary = library;
                if (File.Exists(library))
                {
                    mutableLibrary = Path.GetFullPath(mutableLibrary);
                }

                libCommandFileContent += '"' + mutableLibrary.Replace("\\", @"\\") + '"';
                libCommandFileContent += " ";
            }

            var tempFile = Path.GetTempFileName();
            using (var commandFile = File.OpenWrite(tempFile))
            {
                using var writer = new StreamWriter(commandFile);
                writer.Write(libCommandFileContent);
            }

            Console.WriteLine("Launching lib.exe with command file content {0}", libCommandFileContent);
            Directory.SetCurrentDirectory(binaryDir);
            var p = new ProcessStartInfo()
            {
                Arguments = $"@\"{tempFile}\"",
                FileName = libExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = binaryDir,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            var process = new Process();
            process.StartInfo = p;
            process.Start();
            Console.WriteLine(process.StandardOutput.ReadToEnd());
            Console.WriteLine(process.StandardError.ReadToEnd());
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Console.WriteLine("LIB.exe failed, exit code: {0}", process.ExitCode);
                return;
            }

            Console.WriteLine("LIB.exe finished");
            var objFiles = Directory.GetFiles(binaryDir, "*.obj");
            foreach (var file in objFiles)
                File.Delete(file);
        }
    }
}