using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ebuild.api;
using ebuild.api.Toolchain;
using ebuild.Platforms;
using ebuild.Toolchains;
using Microsoft.Extensions.Logging;

namespace ebuild.Modules;


public class SingletonRegistry<T, U> where U : SingletonRegistry<T, U>, new()
{

    public void Register(T item)
    {
        RegisteredItems.Add(item);
    }

    public void Unregister(T item)
    {
        RegisteredItems.Remove(item);
    }

    protected List<T> RegisteredItems { get; } = [];

    public IEnumerable<T> GetAll()
    {
        return RegisteredItems;
    }

    public static U Instance => GetInstance();

    private static U GetInstance()
    {
        if (!_registries.TryGetValue(typeof(U), out var registryObj))
        {
            var registry = new U();
            _registries[typeof(U)] = registry;
            return registry;
        }
        return (U)registryObj;
    }

    private static Dictionary<Type, object> _registries = [];
}

[AutoRegisterService(typeof(ModuleFileGeneratorRegistry))]
public class ModuleFileGeneratorRegistry : SingletonRegistry<IModuleFileGenerator, ModuleFileGeneratorRegistry>
{
    public static void RegisterAllInAssembly(Assembly assembly)
    {
        var generatorTypes = assembly.GetTypes().Where(t => typeof(IModuleFileGenerator).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
        foreach (var generatorType in generatorTypes)
        {
            var generatorInstance = (IModuleFileGenerator)Activator.CreateInstance(generatorType)!;
            Instance.Register(generatorInstance);
        }
    }
}


public class DefaultModuleFileGenerator : IModuleFileGenerator
{
    public string Name => "default";

    public void Generate(string moduleFilePath, bool force, Dictionary<string, string>? templateOptions = null)
    {
        var resolvedModuleFilePath = IModuleFile.TryDirToModuleFile(moduleFilePath, out var name);
        EBuild.RootLogger.LogDebug("Generating module file '{ModuleFilePath}' with name '{ModuleName}'", resolvedModuleFilePath, name);
        if (string.IsNullOrEmpty(resolvedModuleFilePath) && (moduleFilePath is "." or "./" or ".\\"))
        {
            resolvedModuleFilePath = "index.ebuild.cs";
            name = Path.GetDirectoryName(Path.GetFullPath(moduleFilePath)) ?? throw new InvalidOperationException("Failed to determine module name from path.");
        }
        if (File.Exists(resolvedModuleFilePath) && !force)
        {
            throw new InvalidOperationException($"Module file '{resolvedModuleFilePath}' already exists. Use --force to overwrite.");
        }
        var content =
$@"namespace Modules;
using ebuild.api;

public class {name} : ModuleBase
{{
    public {name}(ModuleContext ctx) : base(ctx)
    {{
        this.Name = ""{name}"";
        this.Type = ModuleType.Executable;
        this.CppStandard = CppStandard.Cpp20;

        this.SourceFiles.AddRange(ModuleUtilities.GetAllSourceFiles(this, ""src"", "".cpp"", "".c"", "".cxx""));
        this.Includes.Public.Add(""include"");
        // Include the source directory as well for private includes. This helps cleaner includes in some cases.
        this.Includes.Private.Add(""src"");

        if(context.Toolchain.Name == ""msvc"")
        {{
            // Uncomment to suppress warnings about unsafe functions like strcpy, sprintf, etc. on MSVC
            // Definitions.Public.Add(""_CRT_SECURE_NO_WARNINGS=1"");
            this.Definitions.Public.Add(""UNICODE=1"");
            this.Definitions.Public.Add(""_UNICODE=1"");

            this.CompilerOptions.Add(""/diagnostics:caret"");

        }}


        this.Definitions.Private.Add(""BUILDING_{name.ToUpper()}_MODULE=1"");

    }}
}}
";
        File.WriteAllText(resolvedModuleFilePath, content, Encoding.UTF8);
    }

    public void UpdateSolution(string moduleFilePath)
    {
        try
        {
            var moduleFile = ModuleFile.Get(new ModuleReference(moduleFilePath));
            ModuleInstancingParams instancingParams = new ModuleInstancingParams
            {
                SelfModuleReference = new ModuleReference(moduleFilePath),
                Toolchain = IToolchainRegistry.Get().GetDefaultToolchain() ?? throw new Exception("No default toolchain found"),
                AdditionalCompilerOptions = [],
                Configuration = "Debug",
                Platform = PlatformRegistry.GetHostPlatform(),
                AdditionalDependencyPaths = [],
                AdditionalLinkerOptions = [],
                Architecture = RuntimeInformation.OSArchitecture,
                Options = []
            };
            // First create the module so the module can be read.
            moduleFile.BuildOrGetBuildGraph(instancingParams).Wait();
            // Then create or update the solution.
            moduleFile.CreateOrUpdateSolution(true).Wait();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update solution for module file '{moduleFilePath}': {ex.Message}", ex);
        }
    }



}