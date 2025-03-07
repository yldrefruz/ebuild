using System.CommandLine.Invocation;
using System.Reflection;
using ebuild.api;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild.Compilers;

public class CompilerRegistry
{
    public class ConstructorNotFoundException : Exception
    {
        public readonly string Name;
        public readonly Type CompilerType;

        public ConstructorNotFoundException(Type compilerType, string name) : base(
            $"Compiler {name}'s constructor `{compilerType.Name}()` not found.")
        {
            Name = name;
            CompilerType = compilerType;
        }
    }

    public class CompilerNotFoundException : Exception
    {
        public readonly string Name;

        public CompilerNotFoundException(string name) : base(
            $"Compiler {name} is not found.")
        {
            Name = name;
        }
    }


    public class CompilerAttributeNotFoundException : Exception
    {
        public readonly Type ForType;

        public CompilerAttributeNotFoundException(Type forType) : base($"{forType} doesn't have Compiler attribute")
        {
            ForType = forType;
        }
    }

    private static readonly CompilerRegistry Instance = new();

    public static CompilerRegistry GetInstance() => Instance;

    public class CompilerInstancingParams(string moduleFile)
    {
        public readonly string ModuleFile = moduleFile;
        public string Configuration = Config.Get().DefaultBuildConfiguration;
        public string CompilerName = PlatformRegistry.GetHostPlatform().GetDefaultCompilerName() ?? "Null";
        public ILogger? Logger;
        public List<string>? AdditionalCompilerOptions;
        public List<string>? AdditionalLinkerOptions;

        /// <summary>
        /// Create a CompilerInstancingParams from the command line arguments and options.
        /// The logger will be null if created this way
        /// </summary>
        /// <param name="context">the context to use for creation</param>
        /// <returns></returns>
        public static CompilerInstancingParams FromOptionsAndArguments(InvocationContext context)
        {
            return new CompilerInstancingParams(
                context.ParseResult.GetValueForArgument(CompilerCreationUtilities.ModuleArgument))
            {
                Configuration = context.ParseResult.GetValueForOption(CompilerCreationUtilities.ConfigurationOption) ??
                                "Null",
                Logger = null,
                CompilerName = context.ParseResult.GetValueForOption(CompilerCreationUtilities.CompilerName) ??
                               GetDefaultCompilerName(),
                AdditionalCompilerOptions =
                    context.ParseResult.GetValueForOption(CompilerCreationUtilities.AdditionalCompilerOptions),
                AdditionalLinkerOptions =
                    context.ParseResult.GetValueForOption(CompilerCreationUtilities.AdditionalLinkerOptions)
            };
        }
    }

    public static string GetDefaultCompilerName() => Config.Get().PreferredCompilerName ??
                                                     PlatformRegistry.GetHostPlatform().GetDefaultCompilerName() ??
                                                     "Null";

    public static async Task<CompilerBase> CreateInstanceFor(CompilerInstancingParams instancingParams)
    {
        var moduleContext = new ModuleContext(new FileInfo(instancingParams.ModuleFile),
            instancingParams.Configuration,
            PlatformRegistry.GetHostPlatform(),
            instancingParams.CompilerName,
            null);
        var moduleFile = new ModuleFile(instancingParams.ModuleFile);
        var createdModule = await moduleFile.CreateModuleInstance(moduleContext);
        var compiler = await GetInstance().Create(instancingParams.CompilerName);
        instancingParams.Logger?.LogInformation("Compiler for module {module_name} is {compiler_name}({compiler_path})",
            createdModule.Name ?? createdModule.GetType().Name, compiler.GetName(),
            compiler.GetExecutablePath());
        compiler.SetModule(createdModule);
        var targetWorkingDir = Path.Join(moduleFile.Directory, "Binaries");
        Directory.CreateDirectory(targetWorkingDir);
        Directory.SetCurrentDirectory(targetWorkingDir);
        if (instancingParams.AdditionalCompilerOptions != null)
            compiler.AdditionalCompilerOptions.AddRange(instancingParams.AdditionalCompilerOptions!);
        if (instancingParams.AdditionalLinkerOptions != null)
            compiler.AdditionalLinkerOptions.AddRange(instancingParams.AdditionalLinkerOptions!);
        return compiler;
    }

    public async Task<CompilerBase> Create(string name)
    {
        if (!_compilerTypeList.TryGetValue(name, out var compilerType))
        {
            throw new CompilerNotFoundException(name);
        }

        var constructorInfo =
            compilerType.GetConstructor(Array.Empty<Type>());
        if (constructorInfo == null)
            throw new ConstructorNotFoundException(compilerType, name);

        var created = (CompilerBase)constructorInfo.Invoke(Array.Empty<object?>());
        if (!await created.Setup())
        {
            throw new ApplicationException("Compiler was created successfully, but setup was failed.");
        }

        return created;
    }

    public Task<CompilerBase> Create(Type type)
    {
        return Create(GetNameOfCompiler(type));
    }

    public Task<T>? Create<T>() where T : CompilerBase
    {
        return Create(typeof(T)) as Task<T>;
    }


    public string GetNameOfCompiler(Type compilerType)
    {
        if (compilerType.IsSubclassOf(typeof(CompilerBase)))
        {
            if (compilerType.GetCustomAttribute(typeof(CompilerAttribute)) is CompilerAttribute ca)
            {
                return ca.GetName();
            }
            else
                throw new CompilerAttributeNotFoundException(compilerType);
        }
        else
            throw new ArgumentException($"{compilerType.Name} isn't subclass of CompilerBase.");
    }

    public string GetNameOfCompiler<T>() where T : CompilerBase
    {
        return GetNameOfCompiler(typeof(T));
    }


    public void Register<T>() where T : CompilerBase
    {
        Register(typeof(T));
    }

    public void Register(Type compilerType)
    {
        if (!compilerType.IsSubclassOf(typeof(CompilerBase)))
        {
            throw new ArgumentException("Invalid compiler type. compilerType must be child of `CompilerBase`");
        }

        string name;
        if (compilerType.GetCustomAttribute(typeof(CompilerAttribute)) is CompilerAttribute ca)
        {
            name = ca.GetName();
        }
        else
        {
            throw new CompilerAttributeNotFoundException(compilerType);
        }

        _compilerTypeList.Add(name, compilerType);
    }

    public void RegisterAllFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(CompilerBase))) continue;
            if (type.GetCustomAttribute<CompilerAttribute>() != null)
            {
                Register(type);
            }
        }
    }


    private readonly Dictionary<string, Type> _compilerTypeList = new();
}