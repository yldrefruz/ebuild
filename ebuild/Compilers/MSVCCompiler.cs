using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Compilers;

[Compiler("Msvc")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class MsvcCompiler : CompilerBase
{
    private string _msvcCompilerRoot = string.Empty;
    private string _msvcToolRoot = string.Empty;

    private static readonly ILogger Logger =
        LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = true;
            }))
            .CreateLogger("MSVC Compiler");

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


    public override string GetExecutablePath()
    {
        var msvcCompilerBin = GetMsvcCompilerBin();
        var clPath = Path.Join(msvcCompilerBin, "cl.exe");
        if (clPath.Contains(' '))
        {
            clPath = "\"" + clPath + "\"";
        }

        return clPath;
    }

    private string GetMsvcCompilerBin()
    {
        var targetArch = "x86";
        if (CurrentModule is { Context.TargetArchitecture: Architecture.X64 })
            targetArch = "x64";
        var msvcCompilerBin = Path.Join(_msvcCompilerRoot, targetArch);
        return msvcCompilerBin;
    }

    private string GetMsvcCompilerLib()
    {
        var targetArch = "x86";
        if (CurrentModule is { Context.TargetArchitecture: Architecture.X64 })
            targetArch = "x64";
        return Path.Join(_msvcToolRoot, "lib", targetArch);
    }

    private static string GetShorterPath(string path)
    {
        var fp = Path.GetFullPath(path).Replace("\\", @"\\");
        //We are in binary, so we should resolve the path from the binary folder.
        var rp = Path.GetRelativePath(Path.Join(Directory.GetCurrentDirectory(), "Binaries"), path)
            .Replace("\\", @"\\");
        return fp.Length > rp.Length ? rp : fp;
    }

    // ReSharper disable once UnusedParameter.Local
    private void MutateTarget()
    {
        if (CurrentModule == null)
            return;

        CurrentModule.Includes.Public.AddRange(new[]
        {
            new IncludeDirectory(Path.Join(_msvcToolRoot, "include")),
            //TODO: programatically find this.
            new IncludeDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt"),
            new IncludeDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um"),
            new IncludeDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\shared"),
            new IncludeDirectory(@"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\winrt")
        });

        CurrentModule.LibrarySearchPaths.Public.AddRange(new[]
        {
            @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\um\x64",
            @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt\x64",
            @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt_enclave\x64",
            GetMsvcCompilerLib()
        });
    }

    private string CppStandardToArg(CppStandards standard)
    {
        var value = "/std:";
        switch (standard)
        {
            case CppStandards.Cpp14:
                value += "c++14";
                break;
            case CppStandards.Cpp17:
                value += "c++17";
                break;
            default:
            case CppStandards.Cpp20:
                value += "c++20";
                break;
            case CppStandards.CppLatest:
                value += "c++latest";
                break;
        }

        return value;
    }

    private string GenerateCompileCommand(bool bSourceFiles)
    {
        if (CurrentModule == null) throw new NullReferenceException();
        // ReSharper disable once StringLiteralTypo
        var command = "/nologo /c /EHsc  " + CppStandardToArg(CurrentModule.CppStandard) + " ";
        if (CurrentModule.Context.BuildType.ToLowerInvariant() == "debug")
        {
            command += "/MDd ";
        }
        else
        {
            command += "/MD ";
        }

        if (AdditionalFlags.Count != 0)
        {
            command += string.Join(" ", AdditionalFlags);
            command += " ";
        }


        foreach (var definition in CurrentModule.Definitions.Joined())
        {
            command += $"/D\"{definition}\"";
            command += " ";
        }

        var binaryDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Directory.GetParent(binaryDir)!.FullName);
        foreach (var toInclude in CurrentModule.Includes.Joined()
                     .Select(include => $"/I\"{GetShorterPath(include.Directory)}\""))
        {
            command += toInclude;
            command += " ";
        }

        foreach (var inc in CurrentModule.ForceIncludes.Joined())
        {
            var finalInc = GetShorterPath(inc);
            if (inc.Contains(' ') && !inc.Trim().StartsWith('\"'))
            {
                finalInc = '"' + finalInc + '"';
            }

            command += $"/FI {finalInc} ";
        }

        if (bSourceFiles)
        {
            foreach (var source in CurrentModule.SourceFiles)
            {
                command += '"' + GetShorterPath(source) + '"';
                command += " ";
            }
        }

        Directory.SetCurrentDirectory(binaryDir);

        return command;
    }

    public override async Task<bool> Setup()
    {
        if (!VswhereExists())
        {
            if (!DownloadVsWhere())
            {
                throw new Exception(
                    $"Can't download vswhere from {VsWhereUrl}. Please check your internet connection.");
            }
        }

        var toolRoot = Config.Get().MsvcPath ?? string.Empty;
        if (string.IsNullOrEmpty(toolRoot))
        {
            var vsWhereExecutable = Path.Join(GetVsWhereDirectory(), "vswhere.exe");
            const string args =
                "-latest -products * -requires \"Microsoft.VisualStudio.Component.VC.CoreBuildTools\" -property installationPath";
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

        toolRoot = toolRoot.Trim();


        var version = Config.Get().MsvcVersion ?? string.Empty;
        version = version.Trim();
        if (!File.Exists(Path.Join(toolRoot, "VC", "Tools", "MSVC", version)))
        {
            Logger.LogInformation("(Config) => Msvc Version: {version} is not found, trying to find a valid version.",
                string.IsNullOrEmpty(version) ? version : "<Empty>");
        }

        if (string.IsNullOrEmpty(version))
        {
            Dictionary<Version, string> versionDict = new();
            foreach (var file in Directory.GetFiles(Path.Join(toolRoot, "VC",
                         "Auxiliary", "Build"), "Microsoft.VCToolsVersion.*default.txt"))
            {
                var content = await File.ReadAllTextAsync(file);
                if (Version.TryParse(content, out var foundVer))
                {
                    versionDict.Add(foundVer, content);
                    using (Logger.BeginScope("Version Discovery"))
                    {
                        Logger.LogInformation("Found version: {content}", content);
                    }
                }
            }

            var latestVer = versionDict.Keys.ToList().OrderDescending().FirstOrDefault();
            if (latestVer != null) version = versionDict[latestVer];
        }

        version = version.Trim();
        if (string.IsNullOrEmpty(version))
        {
            Logger.LogCritical("Couldn't find a valid msvc installation.");
            return false;
        }

        _msvcToolRoot = Path.Join(toolRoot, "VC", "Tools", "MSVC", version);
        var host = "Hostx86";
        if (Environment.Is64BitOperatingSystem)
        {
            host = "Hostx64";
        }

        _msvcCompilerRoot = Path.Join(_msvcToolRoot, "bin", host);
        return true;
    }

    public override async Task<bool> Compile()
    {
        if (CurrentModule == null) return false;
        Logger.LogInformation("Compiling program");
        MutateTarget();
        var commandFileContent = GenerateCompileCommand(true);

        var commandFilePath = Path.GetTempFileName();
        await using (var commandFile = File.OpenWrite(commandFilePath))
        {
            await using var writer = new StreamWriter(commandFile);
            await writer.WriteAsync(commandFileContent);
            await writer.FlushAsync();
            commandFile.Flush();
        }

        //Delete all obj files before compiling
        ClearObjectAndPdbFiles(false);

        var startInfo = new ProcessStartInfo()
        {
            WorkingDirectory = Directory.GetCurrentDirectory(),
            Arguments = $"@\"{commandFilePath}\"",
            FileName = GetExecutablePath(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        var proc = Process.Start(startInfo);
        if (proc == null)
        {
            Logger.LogError("Can't start cl.exe");
            Environment.ExitCode = 1;
            return false;
        }

        proc.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null)
            {
                Logger.LogInformation("{data}", args.Data);
            }
        };
        proc.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) Logger.LogError("{data}", args.Data);
        };
        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();
        await proc.WaitForExitAsync();

        if (File.Exists(commandFilePath))
            File.Delete(commandFilePath);
        if (proc.ExitCode != 0)
        {
            Logger.LogError("Compilation Failed, {exitCode}", proc.ExitCode);
            ClearObjectAndPdbFiles();

            return false;
        }

        Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName);
        switch (CurrentModule.Type)
        {
            case ModuleType.StaticLibrary:
            {
                CallLibExe();
                break;
            }
            case ModuleType.DynamicLibrary:
            case ModuleType.Executable:
            case ModuleType.ExecutableWin32:
            {
                CallLinkExe();
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        ProcessAdditionalDependencies();
        return true;
    }

    private static void ClearObjectAndPdbFiles(bool shouldLog = true)
    {
        List<string> files = new();
        files.AddRange(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.out", SearchOption.TopDirectoryOnly));
        files.AddRange(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.pdb", SearchOption.TopDirectoryOnly));
        foreach (var file in files)
        {
            if (shouldLog)
                Logger.LogDebug("Compilation file {file} is being removed", file);
            try
            {
                File.Delete(Path.GetFullPath(file));
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    private void ProcessAdditionalDependencies()
    {
        Logger.LogInformation("Copying files/directories");
        foreach (var additionalDependency in CurrentModule!.AdditionalDependencies.Joined())
        {
            switch (additionalDependency.Type)
            {
                case AdditionalDependency.DependencyType.Directory:
                {
                    var dir = new DirectoryInfo(additionalDependency.Path);
                    var targetDir = additionalDependency.Target ??
                                    Path.Join(Directory.GetCurrentDirectory(), "Binaries");
                    Directory.CreateDirectory(targetDir);
                    var files = dir.GetFiles("*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var targetFile = Path.Combine(targetDir,
                            Path.GetRelativePath(additionalDependency.Path, file.FullName));
                        var parentDir = new FileInfo(targetFile).Directory;
                        while (parentDir is { Exists: false })
                        {
                            parentDir.Create();
                            parentDir = parentDir.Parent;
                        }

                        if (additionalDependency.Processor != null)
                        {
                            additionalDependency.Processor(additionalDependency.Path, targetFile);
                        }
                        else
                        {
                            File.Copy(file.FullName, targetFile, true);
                        }
                    }

                    break;
                }
                case AdditionalDependency.DependencyType.File:
                {
                    var targetDir = additionalDependency.Target ??
                                    Path.Join(Directory.GetCurrentDirectory(), "Binaries");
                    Directory.CreateDirectory(targetDir);
                    var file = new FileInfo(additionalDependency.Path);
                    var targetFile = Path.Combine(targetDir,
                        Path.GetRelativePath(additionalDependency.Path, file.FullName));
                    var parentDir = new FileInfo(targetFile).Directory;
                    while (parentDir is { Exists: false })
                    {
                        parentDir.Create();
                        parentDir = parentDir.Parent;
                    }

                    if (additionalDependency.Processor != null)
                    {
                        additionalDependency.Processor(additionalDependency.Path, targetFile);
                    }
                    else
                    {
                        File.Copy(additionalDependency.Path, targetFile, true);
                    }

                    break;
                }
            }
        }
    }

    private void CallLinkExe()
    {
        if (CurrentModule == null)
            return;
        var binaryDir = Path.Join(Directory.GetCurrentDirectory(), "Binaries");
        Logger.LogInformation("Linking program");
        var libExe = Path.Join(GetMsvcCompilerBin(), "link.exe");
        var files = Directory.GetFiles(Path.Join(Directory.GetCurrentDirectory(), "Binaries"));
        files = files.Where(s => s.EndsWith(".obj")).ToArray();
        files = files.Select(GetShorterPath).ToArray();
        // ReSharper disable once StringLiteralTypo
        var libCommandFileContent = "/nologo /verbose  ";
        var outType = ".exe";
        switch (CurrentModule.Type)
        {
            case ModuleType.ExecutableWin32:
                libCommandFileContent += "/SUBSYSTEM:WINDOWS ";
                break;
            case ModuleType.Executable:
                libCommandFileContent = "/SUBSYSTEM:CONSOLE ";
                break;
            case ModuleType.DynamicLibrary:
                libCommandFileContent = "/DLL ";
                outType = ".dll";
                break;
            case ModuleType.StaticLibrary:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        libCommandFileContent +=
            $"/OUT:\"{Path.Join(binaryDir, CurrentModule.Name + outType)}\" ";

        foreach (var libPath in CurrentModule.LibrarySearchPaths.Joined())
        {
            // ReSharper disable once StringLiteralTypo
            libCommandFileContent += $"/LIBPATH:\"{Path.GetFullPath(libPath)}\"";
            libCommandFileContent += " ";
        }

        foreach (var file in files)
        {
            libCommandFileContent += $"\"{file}\"";
            libCommandFileContent += " ";
        }

        foreach (var library in CurrentModule.Libraries.Joined())
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

        using (Logger.BeginScope("Linking"))
        {
            Logger.LogDebug("Launching link.exe with command file content {libCommandFileContent}",
                libCommandFileContent);
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
            process.OutputDataReceived += (_, args) =>
            {
                //Ultra basic parsing for output
                //TODO: change the parsing method. Maybe regex.
                if (args.Data == null) return;
                var splitData = args.Data.Split(":");
                if (splitData.Length <= 2) return;
                var errorWords = new[] { "error", "fatal error" };
                var warningWords = new[] { "warning" };
                if (errorWords.Any(word => splitData[1].Trim().StartsWith(word)))
                {
                    Logger.LogError("{data}", args.Data);
                    return;
                }

                if (warningWords.Any(word => splitData[1].Trim().StartsWith(word)))
                {
                    Logger.LogWarning("{data}", args.Data);
                    return;
                }

                Logger.LogInformation("{data}", args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null) Logger.LogError("{data}", args.Data);
            };
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                Logger.LogError("link failed, exit code: {exitCode}", process.ExitCode);
                Directory.SetCurrentDirectory(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent!.FullName);
                return;
            }

            var objFiles = Directory.GetFiles(binaryDir, "*.obj");
            foreach (var file in objFiles)
                File.Delete(file);
            Directory.SetCurrentDirectory(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent!.FullName);
        }
    }

    private void CallLibExe()
    {
        if (CurrentModule == null)
            return;
        var binaryDir = Directory.GetCurrentDirectory();
        var libExe = Path.Join(GetMsvcCompilerBin(), "lib.exe");
        var files = Directory.GetFiles(Path.Join(Directory.GetCurrentDirectory(), "Binaries"));
        files = files.Where(s => s.EndsWith(".obj")).ToArray();
        files = files.Select(GetShorterPath).ToArray();
        Directory.CreateDirectory(Path.Join(binaryDir, "lib"));
        // ReSharper disable once StringLiteralTypo
        var libCommandFileContent = "/nologo /verbose  ";
        if (CurrentModule.Type == ModuleType.DynamicLibrary)
        {
            libCommandFileContent += "/DLL ";
        }

        libCommandFileContent +=
            $"/OUT:\"{Path.Join(binaryDir, "lib", CurrentModule.Name + ".lib")}\" ";

        foreach (var libPath in CurrentModule.LibrarySearchPaths.Joined())
        {
            // ReSharper disable once StringLiteralTypo
            libCommandFileContent += $"/LIBPATH:\"{Path.GetFullPath(libPath)}\"";
            libCommandFileContent += " ";
        }

        foreach (var file in files)
        {
            libCommandFileContent += $"\"{file}\"";
            libCommandFileContent += " ";
        }

        foreach (var library in CurrentModule.Libraries.Joined())
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

        using (Logger.BeginScope("Lib"))
        {
            Logger.LogError("Launching lib.exe with command file content {libCommandFileContent}",
                libCommandFileContent);
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
            process.OutputDataReceived += (_, args) => Logger.LogInformation("{data}", args.Data);
            process.OutputDataReceived += (_, args) => Logger.LogError("{data}", args.Data);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.WaitForExit();

            void CleanupLib()
            {
                var objFiles = Directory.GetFiles(binaryDir, "*.obj");
                foreach (var file in objFiles) File.Delete(file);
            }

            if (process.ExitCode != 0)
            {
                Logger.LogError("LIB.exe failed, exit code: {exitCode}", process.ExitCode);
                Environment.ExitCode = -1;
                CleanupLib();
                return;
            }

            CleanupLib();
        }
    }


    public override async Task<bool> Generate(string what)
    {
        if (what == "CompileCommandsJSON")
        {
            return await GenerateCompileCommands();
        }

        return false;
    }


    private async Task<bool> GenerateCompileCommands()
    {
        var command = GenerateCompileCommand(false);
        command = command.Replace(@"\\", @"\");
        command += "/D__CLANGD__ ";
        if (CurrentModule == null)
            return false;
        switch (CurrentModule.CppStandard)
        {
            case CppStandards.Cpp14:
                command += "/D_MSVC_LANG=201402L ";
                break;
            case CppStandards.Cpp17:
                command += "/D_MSVC_LANG=201703L ";
                break;
            case CppStandards.Cpp20:
                command += "/D_MSVC_LANG=202002L ";
                break;
            case CppStandards.CppLatest:
                command += "/D_MSVC_LANG=202410L ";
                break;
        }

        var jsonArr =
            CurrentModule.SourceFiles.Select(source => new JsonObject
            {
                { "directory", Directory.GetCurrentDirectory() },
                { "command", GetExecutablePath() + " " + command + " " + $"\"{source}\"" },
                { "file", source }
            });
        var serialized = JsonSerializer.Serialize(jsonArr, CompileCommandsJsonSerializerOptions);
        await File.WriteAllTextAsync(
            Path.Join(CurrentModule.Context.ModuleDirectory?.FullName ?? "./", "compile_commands.json"),
            serialized);
        return true;
    }

    private static readonly JsonSerializerOptions CompileCommandsJsonSerializerOptions = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public override bool IsAvailable(PlatformBase platform)
    {
        return platform.GetName() == "Win32";
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        throw new NotImplementedException();
    }
}