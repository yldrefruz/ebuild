using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;


namespace ebuild;

public static class EBuild
{
    private static bool _generateCompileCommandsJson;
    private static bool _noCompile;
    private static bool _debug;
    private static string _additionalFlagsArg = "";
    private static bool _additionalFlags;
    private static bool _watchGenerate;

    private static ILogger? _logger;

    private static string? CompileModuleFile(string modulePath, string ebuildDllPath)
    {
        var moduleDirectory = Directory.GetParent(modulePath)!.FullName;
        var localEBuildDirectory = Directory.CreateDirectory(Path.Join(moduleDirectory, ".ebuild"));
        var moduleName = Path.GetFileNameWithoutExtension(modulePath);
        var ebuildModuleIndex = moduleName.IndexOf(".ebuild_module", StringComparison.Ordinal);
        if (ebuildModuleIndex != -1)
            moduleName = moduleName.Remove(ebuildModuleIndex);
        var moduleProjectFileLocation = Path.Join(localEBuildDirectory.FullName, "module", moduleName + ".csproj");
        Directory.CreateDirectory(Directory.GetParent(moduleProjectFileLocation)!.FullName);
        var moduleProjectFile = File.Create(moduleProjectFileLocation);
        var writer = new StreamWriter(moduleProjectFile);

        // ReSharper disable StringLiteralTypo
        var moduleProjectContent = $"""
                                    <Project Sdk="Microsoft.NET.Sdk">
                                        <PropertyGroup>
                                            <OutputType>Library</OutputType>
                                            <OutputPath>bin/</OutputPath>
                                            <TargetFramework>net8.0</TargetFramework>
                                            <ImplicitUsings>enable</ImplicitUsings>
                                            <Nullable>enable</Nullable>
                                            <AssemblyName>{moduleName}</AssemblyName>
                                        </PropertyGroup>
                                        <ItemGroup>
                                            <Reference Include="{ebuildDllPath}"/>
                                            <!--<PackageReference Include="System.Text.Json" Version="9.0.0"/>-->
                                            <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0"/>
                                            <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0"/>
                                        </ItemGroup>
                                    </Project>
                                    """;
        // ReSharper restore StringLiteralTypo
        writer.Write(moduleProjectContent);
        writer.Close();
        writer.Dispose();
        moduleProjectFile.Close();
        moduleProjectFile.Dispose();
        File.Copy(modulePath,
            Path.Join(Directory.GetParent(moduleProjectFileLocation)!.FullName, moduleName + ".ebuild_module.cs"),
            true);
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = Directory.GetParent(moduleProjectFileLocation)!.FullName,
            FileName = "dotnet",
            Arguments = $"build {moduleProjectFile.Name} --configuration Release",
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
            if (args.Data != null) _logger?.LogError("{data}", args.Data);
        };
        p.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null) _logger?.LogInformation("{data}", args.Data);
        };
        p.StartInfo = psi;
        p.EnableRaisingEvents = true;
        p.Start();
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            //Error happened
            return null;
        }

        var dllFile = Path.Join(Directory.GetParent(moduleProjectFileLocation)!.FullName, "bin/net8.0",
            moduleName + ".dll");
        var toLoadDllFile = Path.Join(localEBuildDirectory.FullName, moduleName + ".dll");
        File.Copy(dllFile, toLoadDllFile, true);
        return toLoadDllFile;
    }

    private static string? FindEBuildDll()
    {
        var bFound = false;
        var currentDir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
        while (!bFound && currentDir != null)
        {
            if (currentDir.GetFiles("ebuild.dll").Length > 0)
                bFound = true;
            else
                currentDir = Directory.GetParent(currentDir.FullName);
        }

        return !bFound ? null : Path.Join(currentDir!.FullName, "ebuild.dll");
    }

    public static void Main(string[] args)
    {
        using (var factory = LoggerFactory.Create(builder => builder.AddConsole().AddSimpleConsole(options =>
               {
                   options.SingleLine = true;
               })))
        {
            _logger = factory.CreateLogger("EBuild");
        }

        var moduleTarget = args[0];
        for (var i = 0; i < args.Length; ++i)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-GenerateCompileCommands" when !_generateCompileCommandsJson:
                    _logger.LogInformation(
                        "GenerateCompileCommands found, will create compile_commands.json file at the moduleTarget's directory");
                    _generateCompileCommandsJson = true;
                    continue;
                case "-NoCompile" when !_noCompile:
                    _logger.LogInformation("NoCompile found, compilation will not happen");
                    _noCompile = true;
                    continue;
                case "-Debug" when !_debug:
                    _debug = true;
                    _logger.LogInformation("Set to debug build");
                    break;
                case "-AdditionalFlags":
                {
                    i++;
                    if (args.Length >= i) continue;
                    _additionalFlags = true;
                    _additionalFlagsArg = args[i];
                    break;
                }
                case "-WatchFiles" when !_watchGenerate:
                    _watchGenerate = true;
                    _logger.LogInformation("Watching directory for file changes (not implemented yet.)");
                    break;
            }
        }


        var ebuildDll = FindEBuildDll();
        if (ebuildDll == null)
        {
            _logger.LogError("Can't find ebuild.dll, it should be next to ebuild.exe");
            return;
        }

        var toLoadDllFile = CompileModuleFile(moduleTarget, ebuildDll);
        if (toLoadDllFile == null)
        {
            _logger.LogError("Module Compilation Failed");
            Environment.ExitCode = 1;
            return;
        }

        var loadedModuleAssembly = Assembly.LoadFile(toLoadDllFile);
        Type? loadedModuleType = null;
        foreach (var type in loadedModuleAssembly.GetTypes())
        {
            if (type.IsSubclassOf(typeof(Module)))
            {
                loadedModuleType = type;
            }
        }

        if (loadedModuleType == null)
        {
            _logger.LogError("Module subclass can't be found in the provided file");
            Environment.ExitCode = 1;
            return;
        }

        var moduleContext = new ModuleContext()
        {
            ModuleFile = moduleTarget,
            ModuleDirectory = Directory.GetParent(moduleTarget)!.FullName,
            EbuildLocation = Assembly.GetExecutingAssembly().Location,
            Watching = _generateCompileCommandsJson && _watchGenerate
        };
        //TODO: Support external compilers. And move all the compilers to another project (Maybe EBuild.DefaultCompilers project)
        PlatformRegistry.LoadFromAssembly(Assembly.GetExecutingAssembly());
        CompilerRegistry.LoadFromAssembly(Assembly.GetExecutingAssembly());
        var createdModule = (Module)Activator.CreateInstance(loadedModuleType, new object?[] { moduleContext })!;
        var compiler = CompilerRegistry.GetCompiler(createdModule);
        _logger.LogInformation("Compiler for module {0} is {1}({2})", createdModule.Name, compiler.GetName(),
            compiler.GetExecutablePath());
        compiler.SetCurrentTarget(createdModule);
        var targetWorkingDir = Path.Join(Directory.GetParent(moduleTarget)!.FullName, "Binaries");
        Directory.CreateDirectory(targetWorkingDir);
        Directory.SetCurrentDirectory(targetWorkingDir);
        compiler.SetDebugBuild(_debug);
        if (_additionalFlags)
        {
            compiler.AdditionalFlags.AddRange(_additionalFlagsArg.Split(" "));
        }

        if (!_noCompile)
            compiler.Compile(moduleContext);
        if (_generateCompileCommandsJson)
            compiler.Generate("CompileCommandsJSON", moduleContext);
    }
}