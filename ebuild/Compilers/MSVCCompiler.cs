using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        var clPath = Path.Join(msvcCompilerBin, "cl.exe");
        if (clPath.Contains(" "))
        {
            clPath = "\"" + clPath + "\"";
        }

        return clPath;
    }

    private string GetMsvcCompilerBin()
    {
        var targetArch = "x86";
        if (GetCurrentTarget() != null && GetCurrentTarget()!.Architecture == Architecture.X64)
            targetArch = "x64";
        var msvcCompilerBin = Path.Join(_msvcCompilerRoot, targetArch);
        return msvcCompilerBin;
    }

    private string GetMsvcCompilerLib()
    {
        var targetArch = "x86";
        if (GetCurrentTarget() != null && GetCurrentTarget()!.Architecture == Architecture.X64)
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
    private void MutateTarget(ModuleContext moduleContext)
    {
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null)
            return;
        if (currentTarget.UseDefaultIncludes)
        {
            currentTarget.Includes.AddRange(new[]
            {
                Path.Join(_msvcToolRoot, "include"),
                //TODO: programatically find this.
                @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\ucrt",
                @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\um",
                @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.22621.0\shared"
            });
        }

        if (currentTarget.UseDefaultLibraryPaths)
        {
            currentTarget.LibrarySearchPaths.AddRange(new[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\um\x64",
                @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt\x64",
                @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.22621.0\ucrt_enclave\x64",
                GetMsvcCompilerLib()
            });
        }

        if (currentTarget.UseDefaultLibraries)
        {
            currentTarget.Libraries.AddRange(new string[]
                { });
        }
    }

    private string CppStandardToArg(CXXStd std)
    {
        string value = "/std:";
        switch (std)
        {
            case CXXStd.CXX14:
                value += "c++14";
                break;
            case CXXStd.CXX15:
                value += "c++17";
                break;
            case CXXStd.CXX20:
                value += "c++20";
                break;
            case CXXStd.CXXLatest:
                value += "c++latest";
                break;
            case CXXStd.C11:
                value += "c11";
                break;
            case CXXStd.C17:
                value += "c17";
                break;
            case CXXStd.CLatest:
                value += "clatest";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(std), std, null);
        }

        return value;
    }

    private string GenerateCompileCommand(bool bSourceFiles)
    {
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null) throw new NullReferenceException();
        var command = "/nologo /c /EHsc " + CppStandardToArg(currentTarget.CppStandard) + " ";
        if (IsDebugBuild())
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


        foreach (var definition in currentTarget.Definitions)
        {
            command += $"/D\"{definition}\"";
            command += " ";
        }

        var binaryDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Directory.GetParent(binaryDir)!.FullName);
        foreach (var toInclude in currentTarget.Includes.Select(include => $"/I\"{GetShorterPath(include)}\""))
        {
            command += toInclude;
            command += " ";
        }

        if (bSourceFiles)
        {
            foreach (var source in currentTarget.SourceFiles)
            {
                command += '"' + GetShorterPath(source) + '"';
                command += " ";
            }
        }

        Directory.SetCurrentDirectory(binaryDir);

        return command;
    }

    public override void Compile(ModuleContext moduleContext)
    {
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null) return;
        Console.WriteLine("Starting the Compiler section.");
        MutateTarget(moduleContext);
        var commandFileContent = GenerateCompileCommand(true);

        var commandFilePath = Path.GetTempFileName();
        using (var commandFile = File.OpenWrite(commandFilePath))
        {
            using var writer = new StreamWriter(commandFile);
            writer.Write(commandFileContent);
            writer.Flush();
            commandFile.Flush();
        }


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
            Console.WriteLine("Can't start cl.exe");
            return;
        }

        proc.OutputDataReceived += (sender, args) => Console.Out.WriteLine("- [Compiler] : " + args.Data);
        proc.ErrorDataReceived += (sender, args) => Console.Error.WriteLine("- [Compiler] : " + args.Data);
        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();
        proc.WaitForExit();

        if (File.Exists(commandFilePath))
            File.Delete(commandFilePath);
        if (proc.ExitCode != 0)
        {
            Console.WriteLine("Compilation Failed, {0}", proc.ExitCode);
            Console.Out.Flush();
            List<string> files = new();
            files.AddRange(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.out", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(Directory.GetCurrentDirectory(), "*.pdb", SearchOption.TopDirectoryOnly));
            foreach (var file in files)
            {
                Console.WriteLine("Compilation file {0} is being removed", file);
                try
                {
                    File.Delete(Path.GetFullPath(file));
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return;
        }

        Thread.Sleep(500);

        Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName);
        Console.WriteLine("End of compiling.");
        switch (currentTarget.Type)
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
        }

        ProcessAdditionalDependencies();
    }

    private void ProcessAdditionalDependencies()
    {
        Console.WriteLine("Copying files/directories");
        foreach (var additionalDependency in GetCurrentTarget()!.AdditionalDependencies)
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
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null)
            return;
        var binaryDir = Path.Join(Directory.GetCurrentDirectory(), "Binaries");
        Console.WriteLine("Starting LINK.EXE");
        var libExe = Path.Join(GetMsvcCompilerBin(), "link.exe");
        var files = Directory.GetFiles(Path.Join(Directory.GetCurrentDirectory(), "Binaries"));
        files = files.Where(s => s.EndsWith(".obj")).ToArray();
        files = files.Select(GetShorterPath).ToArray();
        var libCommandFileContent = "/nologo /verbose ";
        var outType = ".exe";
        if (currentTarget.Type == ModuleType.ExecutableWin32)
        {
            libCommandFileContent += "/SUBSYSTEM:WINDOWS ";
        }
        else if (currentTarget.Type == ModuleType.Executable)
        {
            libCommandFileContent = "/SUBSYSTEM:CONSOLE ";
        }

        if (currentTarget.Type == ModuleType.DynamicLibrary)
        {
            libCommandFileContent = "/DLL ";
            outType = ".dll";
        }

        libCommandFileContent +=
            $"/OUT:\"{Path.Join(binaryDir, currentTarget.Name + outType)}\" ";

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

        Console.WriteLine("Launching link.exe with command file content {0}", libCommandFileContent);
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
            Console.WriteLine("LINK.exe failed, exit code: {0}", process.ExitCode);
            Directory.SetCurrentDirectory(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent!.FullName);
            return;
        }

        Console.WriteLine("LINK.exe finished");
        var objFiles = Directory.GetFiles(binaryDir, "*.obj");
        foreach (var file in objFiles)
            File.Delete(file);
        Directory.SetCurrentDirectory(new DirectoryInfo(Directory.GetCurrentDirectory()).Parent!.FullName);
    }

    private void CallLibExe()
    {
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null)
            return;
        var binaryDir = Directory.GetCurrentDirectory();
        Console.WriteLine("Starting LIB.EXE");
        var libExe = Path.Join(GetMsvcCompilerBin(), "lib.exe");
        var files = Directory.GetFiles(Path.Join(Directory.GetCurrentDirectory(), "Binaries"));
        files = files.Where(s => s.EndsWith(".obj")).ToArray();
        files = files.Select(GetShorterPath).ToArray();
        Directory.CreateDirectory(Path.Join(binaryDir, "lib"));
        var libCommandFileContent = "/nologo /verbose ";
        if (currentTarget.Type == ModuleType.DynamicLibrary)
        {
            libCommandFileContent += "/DLL ";
        }

        libCommandFileContent +=
            $"/OUT:\"{Path.Join(binaryDir, "lib", currentTarget.Name + ".lib")}\" ";

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

    public override void Generate(string what, ModuleContext moduleContext)
    {
        base.Generate(what, moduleContext);
        if (what == "CompileCommandsJSON")
        {
            GenerateCompileCommands(moduleContext);
        }
    }


    private void GenerateCompileCommands(ModuleContext moduleContext)
    {
        var command = GenerateCompileCommand(false);
        command = command.Replace(@"\\", @"\");
        command += "/D__CLANGD__ ";
        var currentTarget = GetCurrentTarget();
        if (currentTarget == null)
            return;
        switch (currentTarget.CppStandard)
        {
            case CXXStd.CXX14:
                command += "/D_MSVC_LANG=201402L ";
                break;
            case CXXStd.CXX15:
                command += "/D_MSVC_LANG=201703L ";
                break;
            case CXXStd.CXX20:
                command += "/D_MSVC_LANG=202002L ";
                break;
            case CXXStd.CXXLatest:
                command += "/D_MSVC_LANG=202410L ";
                break;
            case CXXStd.C11:
                break;
            case CXXStd.C17:
                break;
            case CXXStd.CLatest:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var jsonArr = new JsonArray();
        foreach (var source in currentTarget.SourceFiles)
        {
            var jsonObj = new JsonObject
            {
                { "directory", Directory.GetCurrentDirectory() },
                { "command", GetExecutablePath() + " " + command + " " + $"\"{source}\"" },
                { "file", source }
            };
            jsonArr.Add(jsonObj);
        }

        JsonSerializerOptions opts = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };
        string serialized = JsonSerializer.Serialize(jsonArr, opts);
        File.WriteAllText(Path.Join(moduleContext.ModuleDirectory, "compile_commands.json"), serialized);
    }
}