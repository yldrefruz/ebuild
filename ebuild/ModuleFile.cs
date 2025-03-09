using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild;

public class ModuleFile
{
    private Type? _moduleType;
    private Assembly? _loadedAssembly;
    private ModuleBase? _compiledModule;


    private static Regex DotnetErrorAndWarningRegex =
        new Regex(@"^(?<path>.*): \s*(?<type>error|warning)\s*(?<code>[A-Z0-9]+):\s*(?<message>.+)$");


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

    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("Module File");

    public async Task<ModuleBase?> CreateModuleInstance(ModuleContext context)
    {
        if (_compiledModule != null)
        {
            Logger.LogDebug("Module {name} instance already exists.", Name);
            return _compiledModule;
        }

        var moduleType = await GetModuleType();
        if (moduleType == null)
        {
            Logger.LogError("Couldn't create module instance since module type was invalid.");
            return null;
        }

        Logger.LogDebug("Module type = {name}", moduleType.Name);
        var constructor = moduleType.GetConstructor(new[] { typeof(ModuleContext) });
        if (constructor == null)
        {
            throw new ConstructorNotFoundException((await GetModuleType())!);
        }

        Logger.LogDebug("Module constructor is: {constructor}", constructor);
        var created = constructor.Invoke(new object?[] { context });
        _compiledModule = (ModuleBase)created;
        return _compiledModule;
    }

    private async Task<Type?> GetModuleType()
    {
        if (_moduleType != null)
        {
            Logger.LogDebug("Module {name} is already calculated. Using cached value.", Name);
            return _moduleType;
        }

        if (_loadedAssembly == null)
        {
            Logger.LogDebug("Compiling module {name} as the assembly is not available", Name);
            try
            {
                _loadedAssembly = await CompileAndLoad();
            }
            catch (ModuleFileCompileException exception)
            {
                Logger.LogError("Can't find the type: {message}", exception.Message);
                return null;
            }
        }

        foreach (var type in _loadedAssembly.GetTypes())
        {
            Logger.LogDebug("Type {name} is found in file {file}.", type.Name, type.Assembly.Location);
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
    public string FilePath => _path;

    public readonly string Name;

    public static ModuleFile Get(string path)
    {
        if (!File.Exists(path)) throw new ModuleFileException(path);
        var fi = new FileInfo(path);
        if (_moduleFileRegistry.TryGetValue(fi.FullName, out var value))
        {
            Logger.LogDebug("Module {path} file was already cached. Using cached value", path);
            return value;
        }

        var mf = new ModuleFile(path);
        _moduleFileRegistry.Add(fi.FullName, mf);
        return mf;
    }

    private ModuleFile(string path)
    {
        _path = Path.GetFullPath(path);
        Name = Path.GetFileNameWithoutExtension(path);
        var lastIndexOfEbuild = Name.LastIndexOf(".ebuild", StringComparison.InvariantCultureIgnoreCase);
        if (lastIndexOfEbuild != -1)
        {
            Name = Name.Remove(lastIndexOfEbuild);
        }
    }

    public async Task<List<ModuleFile>> GetDependencies(string configuration, PlatformBase platform, string compiler,
        AccessLimit accessLimit = AccessLimit.Public)
    {
        List<ModuleFile> l = new();
        ModuleContext selfContext = new(new FileInfo(_path), configuration, platform, compiler, null);
        var module = await CreateModuleInstance(selfContext);
        if (module == null)
            return l;
        l.AddRange(module.Dependencies.GetLimited(accessLimit)
            .Select(dependency => Get(Path.GetFullPath(dependency.GetPureFile(), Directory))));
        return l;
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

    /// <summary>
    /// Compile the module file and load the assembly.
    /// There are massive security concerns about this as the loaded assembly can do whatever it wants.
    /// But since the module files should be from trusted sources and user themselves.
    ///
    /// Surely the user will check what they are using.
    /// </summary>
    /// <returns>the assembly for module file.</returns>
    /// <exception cref="ModuleFileCompileException">The module file couldn't be compiled.</exception>
    private async Task<Assembly> CompileAndLoad()
    {
        var localEBuildDirectory = System.IO.Directory.CreateDirectory(Path.Join(Directory, ".ebuild"));
        var toLoadDllFile = Path.Join(localEBuildDirectory.FullName, "module", Name + ".ebuild_module.dll");
        if (!HasChanged())
            return Assembly.LoadFile(toLoadDllFile);
        var logger = LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options => options.SingleLine = true))
            .CreateLogger("Module File Compiler");
        logger.LogDebug("Compiling file, {path}", _path);
        var ebuildApiDll = EBuild.FindEBuildApiDllPath();
        logger.LogDebug("Found ebuild api dll: {path}", ebuildApiDll);

        var moduleProjectFileLocation =
            Path.Join(localEBuildDirectory.FullName, "module", "intermediate", Name + ".csproj");
        logger.LogDebug("ebuild module {name} project is created at: {path}", Name, moduleProjectFileLocation);
        System.IO.Directory.CreateDirectory(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName);
        await using (var moduleProjectFile = File.Create(moduleProjectFileLocation))
        {
            await using (var writer = new StreamWriter(moduleProjectFile))
            {
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
                                                    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0"/>
                                                    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0"/>
                                                </ItemGroup>
                                            </Project>
                                            """;
                // ReSharper restore StringLiteralTypo
                await writer.WriteAsync(moduleProjectContent);
            }
        }

        var moduleFileCopy = Path.Join(Directory, ".ebuild", "module", "intermediate", Name + ".ebuild_module.cs");
        File.Copy(_path, moduleFileCopy, true);
        Logger.LogDebug("Copied module file {og_file}, to {new_file} as intermediate for compiling", _path,
            moduleFileCopy);
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = Directory,
            FileName = "dotnet",
            Arguments = $"build {moduleProjectFileLocation} --configuration Release",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        logger.LogDebug("Starting dotnet to build the module: {program} {args}", psi.FileName, psi.Arguments);
        var p = new Process();
        p.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) logger.LogError("{data}", args.Data);
        };
        var messages = new HashSet<string>();
        p.OutputDataReceived += (_, args) =>
        {
            if (args.Data == null) return;
            var message = args.Data;
            var match = DotnetErrorAndWarningRegex.Match(message);
            var pathMatch = match.Groups["path"];
            var typeMatch = match.Groups["type"];
            var codeMatch = match.Groups["code"];
            var messageMatch = match.Groups["message"];
            if (match.Success)
            {
                switch (typeMatch.Value)
                {
                    case "error":
                        logger.LogError("{path} : {code} : {message}", pathMatch.Value, codeMatch.Value,
                            messageMatch.Value);
                        break;
                    case "warning":
                        logger.LogWarning("{path} : {code} : {message}", pathMatch.Value, codeMatch.Value,
                            messageMatch.Value);
                        break;
                }
            }
            else
            {
                //logger.LogInformation("{data}", args.Data);
            }
        };
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;

        p.Start();
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            throw new ModuleFileCompileException(_path);
        }


        var dllFile = Path.Join(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName, "bin/net8.0",
            Name + ".dll");
        logger.LogDebug("module {name} dll is at {path}, will be copied to {new_path}", Name, dllFile, toLoadDllFile);
        File.Copy(dllFile, toLoadDllFile, true);
        UpdateCachedEditTime();
        logger.LogDebug("module {name} cache time is updated", Name);
        logger.LogDebug("loading the assembly {dll}", toLoadDllFile);
        return Assembly.LoadFile(toLoadDllFile);
    }


    private static Dictionary<string, ModuleFile> _moduleFileRegistry = new();
}