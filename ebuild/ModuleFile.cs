﻿using System.Diagnostics;
using System.Reflection;
using System.Text;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild;

public class ModuleFile
{
    private Type? _moduleType;
    private Assembly? _loadedAssembly;

    private class ConstructorNotFoundException : Exception
    {
        private Type _type;

        public ConstructorNotFoundException(Type type) : base(
            $"{type.Name}(ModuleContext context) not found.")
        {
            _type = type;
        }
    }

    private class ModuleFileException : Exception
    {
        private string _file;

        public ModuleFileException(string file) : base($"{file} is not a valid module file.")
        {
            _file = file;
        }
    }

    private class ModuleFileCompileException : Exception
    {
        private string _file;

        public ModuleFileCompileException(string file) : base($"{file} could not be compiled.")
        {
            _file = file;
        }
    }

    public async Task<ModuleBase> CreateModuleInstance(ModuleContext context)
    {
        var constructor = (await GetModuleType()).GetConstructor(new[] { typeof(ModuleContext) });
        if (constructor == null)
            throw new ConstructorNotFoundException(await GetModuleType());
        var created = constructor.Invoke(new object?[] { context });
        return (ModuleBase)created;
    }

    private async Task<Type> GetModuleType()
    {
        if (_moduleType != null)
            return _moduleType;
        if (_loadedAssembly == null)
            _loadedAssembly = await CompileAndLoad();
        foreach (var type in _loadedAssembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(ModuleBase))) continue;
            _moduleType = type;
            break;
        }

        if (_moduleType == null)
        {
            throw new ModuleFileException(_path);
        }

        return _moduleType!;
    }

    private readonly string _path;
    public string Directory => System.IO.Directory.GetParent(_path)!.FullName;

    public string Name => Path.GetFileNameWithoutExtension(_path)
        .Remove(_path.LastIndexOf(".ebuild", StringComparison.Ordinal));


    public ModuleFile(string path)
    {
        _path = path;
    }

    public ModuleFile(ModuleReference reference, ModuleBase referencingModule)
    {
        var loc = referencingModule.GetType().Assembly.Location;
        loc = System.IO.Directory.GetParent(loc)!.FullName;
        _path = Path.Join(loc, reference.GetPureFile());
    }


    public bool HasChanged()
    {
        return (GetLastEditTime() == null || GetCachedEditTime() == null) || GetLastEditTime() != GetCachedEditTime();
    }

    private DateTime? GetLastEditTime()
    {
        try
        {
            var fi = new FileInfo(_path);
            return fi.LastWriteTimeUtc;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void UpdateCachedEditTime()
    {
        var lastEditTime = GetLastEditTime();
        if (lastEditTime == null) return;
        var fi = GetCachedEditTimeFile();
        fi.Directory?.Create();
        using var fs = fi.Create();
        fs.Write(Encoding.UTF8.GetBytes(lastEditTime.ToString()!));
    }

    private FileInfo GetCachedEditTimeFile() => new(Path.Join(Directory, ".ebuild", Name, "last_edit.cache"));

    private DateTime? GetCachedEditTime()
    {
        var fi = GetCachedEditTimeFile();
        if (fi.Exists)
        {
            return fi.LastWriteTimeUtc;
        }

        return null;
    }

    private async Task<Assembly> CompileAndLoad()
    {
        var localEBuildDirectory = System.IO.Directory.CreateDirectory(Path.Join(Directory, ".ebuild"));
        var toLoadDllFile = Path.Join(localEBuildDirectory.FullName, "module", Name + ".ebuild_module.dll");
        if (!HasChanged())
            return Assembly.LoadFile(toLoadDllFile);
        var logger = LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options => options.SingleLine = true))
            .CreateLogger("Module File Compiler");

        var ebuildApiDll = EBuild.FindEBuildApiDllPath();

        var moduleProjectFileLocation =
            Path.Join(localEBuildDirectory.FullName, "module", "intermediate", Name + ".csproj");
        System.IO.Directory.CreateDirectory(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName);
        await using (var moduleProjectFile = File.Create(moduleProjectFileLocation))
        {
            await using (var writer = new StreamWriter(moduleProjectFile))
            {
                // ReSharper disable StringLiteralTypo
                var moduleProjectContent = $"""
                                            <Project Sdk="Microsoft.NET.Sdk">
                                                <PropertyGroup>
                                                    <OutputType>Library</OutputType>
                                                    <OutputPath>bin/</OutputPath>
                                                    <TargetFramework>net8.0</TargetFramework>
                                                    <ImplicitUsings>enable</ImplicitUsings>
                                                    <Nullable>enable</Nullable>
                                                    <AssemblyName>{Name}</AssemblyName>
                                                </PropertyGroup>
                                                <ItemGroup>
                                                    <Reference Include="{ebuildApiDll}"/>
                                                    <!--<PackageReference Include="System.Text.Json" Version="9.0.0"/>-->
                                                    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0"/>
                                                    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0"/>
                                                </ItemGroup>
                                            </Project>
                                            """;
                // ReSharper restore StringLiteralTypo
                await writer.WriteAsync(moduleProjectContent);
            }
        }

        File.Copy(_path,
            Path.Join(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName,
                Name + ".ebuild_module.cs"),
            true);

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName,
            FileName = "dotnet",
            Arguments = $"build {moduleProjectFileLocation} --configuration Release",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        var p = new Process();
        p.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) logger.LogError("{data}", args.Data);
        };
        p.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null) logger.LogInformation("{data}", args.Data);
        };
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;
        p.Start();
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            //Error happened
            throw new ModuleFileCompileException(_path);
        }

        var dllFile = Path.Join(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName, "bin/net8.0",
            Name + ".dll");

        File.Copy(dllFile, toLoadDllFile, true);
        UpdateCachedEditTime();
        return Assembly.LoadFile(toLoadDllFile);
    }
}