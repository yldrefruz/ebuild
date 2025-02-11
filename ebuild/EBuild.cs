using System.Reflection;
using ebuild.api;
using ebuild.Compilers;
using ebuild.Platforms;
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

    public static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        builder
            .AddConsole().AddSimpleConsole(options => { options.SingleLine = true; }));

    private static readonly ILogger MainLogger = LoggerFactory.CreateLogger("Main");

    public static string? FindEBuildApiDllPath()
    {
        return typeof(ModuleBase).Assembly.Location; // ModuleBase is in ebuild.api
    }

    private static string GetEBuildDllPath()
    {
        return Assembly.GetExecutingAssembly().Location;
    }

    public static async Task Main(string[] args)
    {
        var moduleTarget = args[0];
        for (var i = 0; i < args.Length; ++i)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-GenerateCompileCommands" when !_generateCompileCommandsJson:
                    MainLogger.LogInformation(
                        "GenerateCompileCommands found, will create compile_commands.json file at the moduleTarget's directory");
                    _generateCompileCommandsJson = true;
                    continue;
                case "-NoCompile" when !_noCompile:
                    MainLogger.LogInformation("NoCompile found, compilation will not happen");
                    _noCompile = true;
                    continue;
                case "-Debug" when !_debug:
                    _debug = true;
                    MainLogger.LogInformation("Set to debug build");
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
                    MainLogger.LogInformation("Watching directory for file changes (not implemented yet.)");
                    break;
            }
        }

        //TODO: Support external compilers. And move all the compilers to another project (Maybe EBuild.DefaultCompilers project)
        PlatformRegistry.GetInstance().RegisterAllFromAssembly(Assembly.GetExecutingAssembly());
        CompilerRegistry.GetInstance().RegisterAllFromAssembly(Assembly.GetExecutingAssembly());


        //TODO cli support:
        //  - build type from cli- platform from cli
        //  - compiler from cli -> config -> fallback_compiler ordering
        //  - output file from cli
        //
        var compilerName = PlatformRegistry.GetHostPlatform().GetDefaultCompilerName()!;
        var moduleContext = new ModuleContext(new FileInfo(moduleTarget), "release", PlatformRegistry.GetHostPlatform(),
            compilerName,
            null);
        var moduleFile = new ModuleFile(moduleTarget);
        var createdModule = await moduleFile.CreateModuleInstance(moduleContext);
        var compiler = await CompilerRegistry.GetInstance().Create(compilerName);
        MainLogger.LogInformation("Compiler for module {module_name} is {compiler_name}({compiler_path})",
            createdModule.Name ?? createdModule.GetType().Name, compiler.GetName(),
            compiler.GetExecutablePath());
        compiler.SetModule(createdModule);
        var targetWorkingDir = Path.Join(moduleFile.Directory, "Binaries");
        Directory.CreateDirectory(targetWorkingDir);
        Directory.SetCurrentDirectory(targetWorkingDir);
        //TODO: compiler.SetDebugBuild(_debug);
        if (_additionalFlags)
        {
            compiler.AdditionalFlags.AddRange(_additionalFlagsArg.Split(" "));
        }

        if (!_noCompile)
            await compiler.Compile();
        if (_generateCompileCommandsJson)
            await compiler.Generate("CompileCommandsJSON");
    }
}