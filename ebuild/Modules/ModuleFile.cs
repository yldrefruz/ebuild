using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ebuild.api;
using ebuild.Modules.BuildGraph;
using Microsoft.Extensions.Logging;

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
        var constructor = moduleType.GetConstructor([typeof(ModuleContext)]) ?? throw new MissingMethodException($"Constructor with parameter of type ModuleContext not found in {moduleType.FullName}");
        ModuleFileLogger.LogDebug("Module constructor is: {constructor}", constructor);
        ModuleContext context = instancingParams.ToModuleContext();
        context.SelfReference = _reference;
        context.Options = instancingParams.Options ?? [];
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
            throw new InvalidOperationException($"Failed to construct module of type {moduleType.FullName} due to errors.");
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
            catch (Exception exception)
            {
                ModuleFileLogger.LogError("Can't find the type: {message}", exception.ToString());
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
            throw new TypeLoadException($"No subclass of ModuleBase found in assembly for module file: {_reference.GetFilePath()}");
        }

        return _moduleType!;
    }

    private readonly ModuleReference _reference;
    private Graph? _buildGraph = null;

    /// <summary>
    /// Creates or gets the build graph for this module
    /// </summary>
    /// <param name="moduleInstancingParams">Module instancing parameters</param>
    /// <returns>The build graph for this module</returns>
    public async Task<Graph?> BuildOrGetBuildGraph(IModuleInstancingParams moduleInstancingParams)
    {
        if (_buildGraph == null)
        {
            var moduleInstance = await CreateModuleInstance(moduleInstancingParams);
            if (moduleInstance == null)
            {
                return null;
            }
            _buildGraph = new Graph(moduleInstance);
        }

        return _buildGraph;
    }

    public readonly string Name;

    public static ModuleFile Get(ModuleReference moduleReference, ModuleReference relativeTo)
    {
        var path = moduleReference.GetFilePath();
        path = Path.GetFullPath(path, Path.GetDirectoryName(relativeTo.GetFilePath())!);
        var f = IModuleFile.TryDirToModuleFile(path, out _);
        if (!File.Exists(f)) throw new FileNotFoundException($"Module file not found: {path}");
        var fi = new FileInfo(f);
        if (ModuleFileRegistry.TryGetValue(fi.FullName, out var value))
        {
            return value;
        }
        var mf = new ModuleFile(moduleReference, relativeTo);
        ModuleFileRegistry.Add(fi.FullName, mf);
        return mf;
    }
    public static ModuleFile Get(ModuleReference moduleReference)
    {
        var path = moduleReference.GetFilePath();
        var f = IModuleFile.TryDirToModuleFile(path, out _);
        if (!File.Exists(f)) throw new FileNotFoundException($"Module file not found: {moduleReference.GetFilePath()}");
        var fi = new FileInfo(f);
        if (ModuleFileRegistry.TryGetValue(fi.FullName, out var value))
        {
            return value;
        }

        var mf = new ModuleFile(moduleReference);
        ModuleFileRegistry.Add(fi.FullName, mf);
        return mf;
    }

    private ModuleFile(ModuleReference reference, ModuleReference relativeTo)
    {
        _reference = new ModuleReference(outputType: reference.GetOutput(),
            path: IModuleFile.TryDirToModuleFile(Path.GetFullPath(reference.GetFilePath(), Path.GetDirectoryName(relativeTo.GetFilePath())!), out var name),
            version: reference.GetVersion(),
            options: reference.GetOptions());
        if (string.IsNullOrEmpty(_reference.GetFilePath()))
        {
            throw new FileNotFoundException($"Module file not found: {_reference.GetFilePath()}");
        }

        Name = name;

        var lastIndexOfEbuild = Name.LastIndexOf(".ebuild", StringComparison.InvariantCultureIgnoreCase);
        if (lastIndexOfEbuild != -1)
        {
            Name = Name.Remove(lastIndexOfEbuild);
        }
    }
    private ModuleFile(ModuleReference reference)
    {
        _reference = new ModuleReference(outputType: reference.GetOutput(),
            path: IModuleFile.TryDirToModuleFile(Path.GetFullPath(reference.GetFilePath()), out var name),
            version: reference.GetVersion(),
            options: reference.GetOptions());
        if (string.IsNullOrEmpty(_reference.GetFilePath()))
        {
            throw new FileNotFoundException($"Module file not found: {_reference.GetFilePath()}");
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
    private static readonly Regex PragmaIncludeRegex = new(@"^\s*#\s*pragma\s+include\s+""(?<path>.+)""\s*$", RegexOptions.Multiline);
    private static readonly Regex PragmaProjectReferenceRegex = new(@"^\s*#\s*pragma\s+project_reference\s+""(?<path>.+)""\s*$", RegexOptions.Multiline);
    public string[] GetIncludes()
    {
        // read file content
        var lines = File.ReadAllLines(_reference.GetFilePath());
        var includes = new List<string>();
        foreach (var line in lines)
        {
            var match = PragmaIncludeRegex.Match(line);
            if (match.Success)
            {
                var includePath = match.Groups["path"].Value;
                includePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_reference.GetFilePath())!, includePath));
                includes.Add(includePath);
            }
        }
        return [.. includes];
    }

    public string[] GetProjectReferences()
    {
        // read file content
        var lines = File.ReadAllLines(_reference.GetFilePath());
        var includes = new List<string>();
        foreach (var line in lines)
        {
            var match = PragmaProjectReferenceRegex.Match(line);
            if (match.Success)
            {
                var includePath = match.Groups["path"].Value;
                includes.Add(includePath);
            }
        }
        return [.. includes];
    }
    public string DotEbuildDir => Path.Join(GetDirectory(), ".ebuild", Name);
    public string CSharpIntermediateDir => Path.Join(DotEbuildDir, "intermediate");
    public string CSharpSolutionFile => Path.Join(CSharpIntermediateDir, $"{Name}.ebuild_module.sln");
    public string CSharpProjectFile => Path.Join(CSharpIntermediateDir, $"{Name}.ebuild_module.csproj");
    public async Task CreateOrUpdateSolution(bool includeDependencies = false)
    {
        // Solutions contains dependencies as well, so we need to create a solution file if it doesn't exist
        Directory.CreateDirectory(CSharpIntermediateDir);
        // If the solution file already is up to date just return
        if (File.Exists(CSharpSolutionFile) && File.Exists(CSharpProjectFile) && !includeDependencies)
            return;
        // Always recreate on the case of include dependencies. So use --force.
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = CSharpIntermediateDir,
            Arguments = $"new sln --name {Name}.ebuild_module --force",
            CreateNoWindow = true,
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var p = new Process { StartInfo = psi };
        p.Start();
        // Discard output
        await p.StandardOutput.ReadToEndAsync();
        await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        List<ModuleBase> dependencies = [];
        if (_buildGraph != null && GetCompiledModule() != null && includeDependencies)
        {
            var worker = _buildGraph.CreateWorker<DependencyCollectorWorker>();
            await (worker as IWorker).ExecuteAsync();
            dependencies.AddRange(worker.AllDependencies.Where(d => d != GetCompiledModule() && d != null));
        }
        psi.Arguments = $"sln {Name}.ebuild_module.sln add \"{CSharpProjectFile}\"";
        if (dependencies.Count > 0)
        {
            foreach (var dependency in dependencies)
            {
                psi.Arguments += $" \"{((ModuleFile)dependency).CSharpProjectFile}\"";
            }
        }
        p = new Process { StartInfo = psi };
        p.Start();
        // Discard output
        await p.StandardOutput.ReadToEndAsync();
        await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
    }


    private static Assembly LoadAssembly(string path)
    {
        var openFile = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] buffer = new byte[openFile.Length];
        openFile.ReadExactly(buffer);
        openFile.Close();
        return Assembly.Load(buffer);
    }

    public bool HasChanged()
    {
        var lastWriteTime = File.GetLastWriteTimeUtc(_reference.GetFilePath());
        var dllWriteTime = DateTime.MinValue;
        try
        {
            var localEBuildDirectory = Directory.CreateDirectory(Path.Join(GetDirectory(), ".ebuild"));
            var toLoadDllFile = Path.Join(localEBuildDirectory.FullName, Name, Name + ".ebuild_module.dll");
            if (File.Exists(toLoadDllFile))
            {
                dllWriteTime = File.GetLastWriteTimeUtc(toLoadDllFile);
            }
        }
        catch
        {
            // Ignore errors
        }
        return lastWriteTime > dllWriteTime;
    }


    private string ToLoadDll => Path.Join(GetDirectory(), ".ebuild", Name, Name + ".ebuild_module.dll");
    private string EBuildCacheDir => Path.Join(GetDirectory(), ".ebuild");

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
        if (!HasChanged())
        {
            return LoadAssembly(ToLoadDll);
        }

        var logger = LoggerFactory
            .Create(builder => builder.AddConsole().AddSimpleConsole(options => options.SingleLine = true))
            .CreateLogger("Module File Compiler");
        logger.LogDebug("Compiling file, {path}", _reference.GetFilePath());
        var ebuildApiDll = EBuild.FindEBuildApiDllPath();
        logger.LogDebug("Found ebuild api dll: {path}", ebuildApiDll);
        var moduleProjectFileDir = Path.Join(EBuildCacheDir, Name, "intermediate");
        var moduleProjectFileLocation =
            Path.Join(moduleProjectFileDir, Name + ".ebuild_module.csproj");
        logger.LogDebug("ebuild module {name} project is created at: {path}", Name, moduleProjectFileLocation);
        Directory.CreateDirectory(Directory.GetParent(moduleProjectFileLocation)!.FullName);

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

                                                <Compile Include="{Path.GetRelativePath(moduleProjectFileDir, _reference.GetFilePath())}"/>
                                                {String.Join("\n", GetIncludes().Select(pr => $@"<Compile Include=""{Path.GetRelativePath(moduleProjectFileDir, pr)}""/>"))}

                                                <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0"/>
                                                <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0"/>
                                                {String.Join("\n", GetProjectReferences().Select(pr => $@"<ProjectReference Include=""{Path.GetRelativePath(moduleProjectFileDir, pr)}""/>"))}
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
        void logMessage(string? data)
        {
            if (data == null) return;
            var message = data;
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
        }
        p.ErrorDataReceived += (_, args) => logMessage(args.Data);
        p.OutputDataReceived += (_, args) => logMessage(args.Data);
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;

        p.Start();
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to compile module file: {_reference.GetFilePath()}");
        }


        var dllFile = Path.Join(System.IO.Directory.GetParent(moduleProjectFileLocation)!.FullName, "bin/net8.0",
            Name + ".ebuild_module.dll");
        logger.LogDebug("module {name} dll is at {path}, will be copied to {new_path}", Name, dllFile, ToLoadDll);
        File.Copy(dllFile, ToLoadDll, true); ;
        logger.LogDebug("module {name} cache time is updated", Name);
        logger.LogDebug("loading the assembly {dll}", ToLoadDll);
        return LoadAssembly(ToLoadDll);
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

    public string GetName()
    {
        return Name;
    }

    private static readonly Dictionary<string, ModuleFile> ModuleFileRegistry = new();

    public static explicit operator ModuleFile(ModuleBase b) => Get(b.Context.SelfReference);
    public static explicit operator ModuleFile(ModuleReference r) => Get(r);
}