using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ebuild.api;
using Microsoft.Extensions.Logging;
using ebuild.api.exceptions;

namespace ebuild;

public class ModuleFile : IModuleFile
{
    private Type? _moduleType;
    private Assembly? _loadedAssembly;
    private ModuleBase? _compiledModule;

    private static readonly Regex DotnetErrorAndWarningRegex =
        new Regex(@"^(?<path>.*): \s*(?<type>error|warning)\s*(?<code>[A-Z0-9]+):\s*(?<message>.+)$");
    private static readonly ILogger ModuleFileLogger = EBuild.LoggerFactory.CreateLogger("Module File");
    private static readonly ILogger ModuleLogger = EBuild.LoggerFactory.CreateLogger("Module");

    public async Task<ModuleBase?> CreateModuleInstance(IModuleInstancingParams instancingParams)
    {
        if (_compiledModule != null)
        {
            ModuleFileLogger.LogDebug("Module {name} instance already exists.", Name);
            return _compiledModule;
        }

        var moduleType = await GetModuleType();
        if (moduleType == null)
        {
            ModuleFileLogger.LogError("Couldn't create module instance since module type was invalid.");
            return null;
        }

        ModuleFileLogger.LogDebug("Module type = {name}", moduleType.Name);
        var constructor = moduleType.GetConstructor(new[] { typeof(ModuleContext) });
        if (constructor == null)
        {
            throw new ModuleConstructorNotFoundException(moduleType);
        }

        ModuleFileLogger.LogDebug("Module constructor is: {constructor}", constructor);
        ModuleContext context = instancingParams.ToModuleContext();
        context.SelfReference = _reference;
        context.Options = instancingParams?.GetOptions() ?? [];
        var created = constructor.Invoke([context]);
        var failed = false;
        foreach (var m in context.Messages)
        {
            switch (m.GetSeverity())
            {
                case ModuleContext.Message.SeverityTypes.Info:
                    ModuleLogger.LogInformation("{message}", m.GetMessage());
                    break;
                case ModuleContext.Message.SeverityTypes.Warning:
                    ModuleLogger.LogWarning("{message}", m.GetMessage());
                    break;
                case ModuleContext.Message.SeverityTypes.Error:
                    ModuleLogger.LogError("{message}", m.GetMessage());
                    failed = true;
                    break;
            }
        }

        if (failed)
        {
            throw new ModuleConstructionFailedException(moduleType);
        }

        _compiledModule = (ModuleBase)created;
        _compiledModule.PostConstruction();
        return _compiledModule;
    }

    public ModuleBase GetCompiledModule() => _compiledModule!;

    private async Task<Type?> GetModuleType()
    {
        if (_moduleType != null)
        {
            ModuleFileLogger.LogDebug("Module {name} is already calculated. Using cached value.", Name);
            return _moduleType;
        }

        if (_loadedAssembly == null)
        {
            ModuleFileLogger.LogDebug("Compiling module {name} as the assembly is not available", Name);
            try
            {
                _loadedAssembly = await CompileAndLoad();
            }
            catch (ModuleFileCompilationFailedException exception)
            {
                ModuleFileLogger.LogError("Can't find the type: {message}", exception.Message);
                return null;
            }
        }

        foreach (var type in _loadedAssembly.GetTypes())
        {
            ModuleFileLogger.LogDebug("Type {name} is found in file {file}.", type.Name, type.Assembly.Location);
            if (!type.IsSubclassOf(typeof(ModuleBase))) continue;
            _moduleType = type;
            break;
        }

        if (_moduleType == null)
        {
            throw new ModuleFileNotFoundException(_reference.GetFilePath());
        }

        return _moduleType!;
    }

    private readonly ModuleReference _reference;
    private readonly DependencyTree _dependencyTree = new();

    public async Task<IDependencyTree?> BuildOrGetDependencyTree(
        IModuleInstancingParams moduleInstancingParams, bool compileModule = true)
    {
        if (_dependencyTree.IsEmpty() && !compileModule)
        {
            return null;
        }

        if (_dependencyTree.IsEmpty())
        {
            await _dependencyTree.CreateFromModuleFile(this, moduleInstancingParams);
        }

        return _dependencyTree;
    }

    public IDependencyTree GetDependencyTree() => _dependencyTree;

    public readonly string Name;

    public static ModuleFile Get(ModuleReference moduleReference)
    {
        var path = moduleReference.GetFilePath();
        var f = IModuleFile.TryDirToModuleFile(path, out _);
        if (!File.Exists(f)) throw new ModuleFileNotFoundException(f);
        var fi = new FileInfo(f);
        if (ModuleFileRegistry.TryGetValue(fi.FullName, out var value))
        {
            ModuleFileLogger.LogDebug("Module {path} file was already cached. Using cached value", path);
            return value;
        }

        var mf = new ModuleFile(moduleReference);
        ModuleFileRegistry.Add(fi.FullName, mf);
        return mf;
    }

    private ModuleFile(ModuleReference reference)
    {
        _reference = new ModuleReference(outputType: reference.GetOutput(),
            path: IModuleFile.TryDirToModuleFile(Path.GetFullPath(reference.GetFilePath()), out var name),
            version: reference.GetVersion(),
            options: reference.GetOptions());
        if (string.IsNullOrEmpty(_reference.GetFilePath()))
        {
            throw new ModuleFileNotFoundException(_reference.GetFilePath());
        }

        Name = name;

        var lastIndexOfEbuild = Name.LastIndexOf(".ebuild", StringComparison.InvariantCultureIgnoreCase);
        if (lastIndexOfEbuild != -1)
        {
            Name = Name.Remove(lastIndexOfEbuild);
        }
    }

    public async Task<List<Tuple<ModuleReference, AccessLimit>>> GetDependencies(
        IModuleInstancingParams instancingParams, AccessLimit? accessLimit = null)
    {
        List<Tuple<ModuleReference, AccessLimit>> l = new();
        var module = await CreateModuleInstance(instancingParams);
        if (module == null)
            return l;
        if (accessLimit is AccessLimit.Public or null)
        {
            l.AddRange(module.Dependencies.GetLimited(AccessLimit.Public)
                .Select(dependency =>
                    new Tuple<ModuleReference, AccessLimit>(dependency,
                        AccessLimit.Public)));
        }

        if (accessLimit is AccessLimit.Private or null)
        {
            l.AddRange(module.Dependencies.GetLimited(AccessLimit.Private)
                .Select(dependency =>
                    new Tuple<ModuleReference, AccessLimit>(dependency,
                        AccessLimit.Private)));
        }

        return l;
    }

    public async Task<bool> HasCircularDependency(IModuleInstancingParams instancingParams)
    {
        var tree = await BuildOrGetDependencyTree(instancingParams);
        return tree != null && tree.HasCircularDependency();
    }

    public bool HasChanged()
    {
        return GetLastEditTime() == null || GetCachedEditTime() == null || GetLastEditTime() != GetCachedEditTime();
    }

    private DateTime? GetLastEditTime()
    {
        try
        {
            var fi = new FileInfo(_reference.GetFilePath());
            return fi.LastWriteTimeUtc;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void UpdateCachedEditTime()
    {
        var lastEditTime = GetLastEditTime();
        if (lastEditTime == null) return;
        var fi = GetCachedEditTimeFile();
        fi.Directory?.Create();
        using var fs = fi.Create();
        fs.Write(Encoding.UTF8.GetBytes(lastEditTime.ToString()!));
    }

    private FileInfo GetCachedEditTimeFile() => new(Path.Join(GetDirectory(), ".ebuild", Name, "last_edit.cache"));

    private DateTime? GetCachedEditTime()
    {
        var fi = GetCachedEditTimeFile();
        if (fi.Exists)
        {
            return fi.LastWriteTimeUtc;
        }

        return null;
    }

    private bool TryGetModuleMetaFileName(out string fileName)
    {
        if (File.Exists(Path.Join(GetDirectory(), $"{Name}.ebuild.meta")))
        {
            fileName = Path.Join(GetDirectory(), $"{Name}.ebuild.meta");
            return true;
        }

        // The short file name only applies to the short names
        if (IsShortFileName(out var t))
        {
            var mFile = Path.Join(GetDirectory(), t.Replace("cs", "meta"));
            if (File.Exists(mFile))
            {
                fileName = mFile;
                return true;
            }
        }

        fileName = string.Empty;
        return false;
    }

    private bool IsShortFileName(out string type)
    {
        var fileName = Path.GetFileName(_reference.GetFilePath());
        if (fileName.Equals("ebuild.cs", StringComparison.InvariantCultureIgnoreCase))
        {
            type = "ebuild.cs";
            return true;
        }

        if (fileName.Equals("index.ebuild.cs", StringComparison.InvariantCultureIgnoreCase))
        {
            type = "index.ebuild.cs";
            return true;
        }

        type = "non-standard";
        return false;
    }

    private ModuleMeta? _meta;

    private ModuleMeta? GetModuleMeta()
    {
        if (_meta != null) return _meta;
        if (!TryGetModuleMetaFileName(out var name)) return null;
        try
        {
            using var fs = new FileStream(name, FileMode.Open);
            _meta = JsonSerializer.Deserialize<ModuleMeta>(fs, JsonSerializerOptions.Default);
        }
        catch (FileNotFoundException)
        {
        }

        return null;
    }

    private async Task CreateOrUpdateSolution()
    {
        var localEBuildDirectory = System.IO.Directory.CreateDirectory(Path.Join(GetDirectory(), ".ebuild"));
        var moduleProjectFileDir = Path.Join(localEBuildDirectory.FullName, Name, "intermediate");
        var moduleProjectFileLocation =
            Path.Join(moduleProjectFileDir, Name + ".ebuild_module.csproj");
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = moduleProjectFileDir,
            Arguments = $"new sln --name {Name}.ebuild_module",
            CreateNoWindow = true,
            FileName = "dotnet",
            UseShellExecute = false
        };

        var p = new Process
        {
            StartInfo = psi
        };
        p.Start();
        await p.WaitForExitAsync();

        psi.Arguments = $"sln add {moduleProjectFileLocation}";
        p = new Process { StartInfo = psi };
        p.Start();
        await p.WaitForExitAsync();
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
        var localEBuildDirectory = System.IO.Directory.CreateDirectory(Path.Join(GetDirectory(), ".ebuild"));
        var toLoadDllFile = Path.Join(localEBuildDirectory.FullName, Name, Name + ".ebuild_module.dll");
        if (!HasChanged())
            return Assembly.LoadFile(toLoadDllFile);
        var logger = LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options => options.SingleLine = true))
            .CreateLogger("Module File Compiler");
        logger.LogDebug("Compiling file, {path}", _reference.GetFilePath());
        var ebuildApiDll = EBuild.FindEBuildApiDllPath();
        logger.LogDebug("Found ebuild api dll: {path}", ebuildApiDll);
        var moduleProjectFileDir = Path.Join(localEBuildDirectory.FullName, Name, "intermediate");
        var moduleProjectFileLocation =
            Path.Join(moduleProjectFileDir, Name + ".ebuild_module.csproj");
        logger.LogDebug("ebuild module {name} project is created at: {path}", Name, moduleProjectFileLocation);
        System.IO.Directory.CreateDirectory(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName);
        await using (var moduleProjectFile = File.Create(moduleProjectFileLocation))
        {
            await using var writer = new StreamWriter(moduleProjectFile);
            var moduleProjectContent = $"""
                                            <Project Sdk="Microsoft.NET.Sdk">
                                                <PropertyGroup>
                                                    <OutputType>Library</OutputType>
                                                    <OutputPath>bin/</OutputPath>
                                                    <TargetFramework>net8.0</TargetFramework>
                                                    <ImplicitUsings>enable</ImplicitUsings>
                                                    <Nullable>enable</Nullable>
                                                    <AssemblyName>{Name + ".ebuild_module"}</AssemblyName>
                                                </PropertyGroup>
                                                <ItemGroup>
                                                    <Reference Include="{ebuildApiDll}"/>
                                                    {GetModuleMeta()?.GetAdditionalReferenceNodes(moduleProjectFileDir, GetDirectory()) ?? string.Empty}
                                                    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0"/>
                                                    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0"/>
                                                </ItemGroup>
                                                <ItemGroup>
                                                    <Compile Include="{Path.GetRelativePath(moduleProjectFileDir, _reference.GetFilePath())}"/>
                                                    {GetModuleMeta()?.GetAdditionalCompileNodes(moduleProjectFileDir, GetDirectory()) ?? string.Empty}
                                                </ItemGroup>
                                                <ItemGroup>
                                                    <Compile Remove="**/*"/>
                                                    <None Remove="**/*"/>
                                                </ItemGroup>
                                            </Project>
                                            """;
            // ReSharper restore StringLiteralTypo
            await writer.WriteAsync(moduleProjectContent);
        }

        await CreateOrUpdateSolution();

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = GetDirectory(),
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
            throw new ModuleFileCompilationFailedException(_reference.GetFilePath());
        }


        var dllFile = Path.Join(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName, "bin/net8.0",
            Name + ".ebuild_module.dll");
        logger.LogDebug("module {name} dll is at {path}, will be copied to {new_path}", Name, dllFile, toLoadDllFile);
        File.Copy(dllFile, toLoadDllFile, true);
        UpdateCachedEditTime();
        logger.LogDebug("module {name} cache time is updated", Name);
        logger.LogDebug("loading the assembly {dll}", toLoadDllFile);
        return Assembly.LoadFile(toLoadDllFile);
    }
    // TODO: This requires a better way to compare variants. And treat the variants as different files.
    public override bool Equals(object? obj)
    {
        if (obj is ModuleFile mf)
        {
            return mf.GetFilePath() == GetFilePath();
        }

        return false;
    }

    public override int GetHashCode()
    {
        return GetSelfReference().GetFilePath().GetHashCode();
    }

    public string GetFilePath() => GetSelfReference().GetFilePath();
    public string GetDirectory() => Path.GetDirectoryName(GetSelfReference().GetFilePath())!;

    public ModuleReference GetSelfReference() => _reference;

    private static readonly Dictionary<string, ModuleFile> ModuleFileRegistry = new();

    public static explicit operator ModuleFile(ModuleBase b) => Get(b.Context.SelfReference);
    public static explicit operator ModuleFile(ModuleReference r) => Get(r);
}