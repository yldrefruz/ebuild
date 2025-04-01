using System.Reflection;
using ebuild.api;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;

namespace ebuild.Compilers;

public class CompilerRegistry
{
    private class ConstructorNotFoundException : Exception
    {
        public ConstructorNotFoundException(Type compilerType, string name) : base(
            $"Compiler {name}'s constructor `{compilerType.Name}()` not found.")
        {
        }
    }

    private class CompilerNotFoundException : Exception
    {
        public CompilerNotFoundException(string name) : base(
            $"Compiler {name} is not found.")
        {
        }
    }


    private class CompilerAttributeNotFoundException : Exception
    {
        public CompilerAttributeNotFoundException(Type forType) : base($"{forType} doesn't have Compiler attribute")
        {
        }
    }

    private static readonly CompilerRegistry Instance = new();

    public static CompilerRegistry GetInstance() => Instance;


    public static string GetDefaultCompilerName() => Config.Get().PreferredCompilerName ??
                                                     PlatformRegistry.GetHostPlatform().GetDefaultCompilerName() ??
                                                     "Null";

    public static async Task<CompilerBase?> CreateInstanceFor(ModuleInstancingParams instancingParams)
    {
        var opts = new Dictionary<string, string>(instancingParams.Options ?? new Dictionary<string, string>());
        foreach (var a in instancingParams.SelfModuleReference.GetOptions())
        {
            opts.Add(a.Key, a.Value);
        }

        var moduleContext = (ModuleContext)instancingParams;
        moduleContext.Options = opts;
        var moduleFile = (ModuleFile)instancingParams.SelfModuleReference;
        var createdModule = await moduleFile.CreateModuleInstance(moduleContext);
        if (createdModule == null)
        {
            instancingParams.Logger?.LogError("Can't create compiler instance.");
            return null;
        }

        var compiler = await GetInstance().Create(instancingParams.CompilerName);
        instancingParams.Logger?.LogInformation("Compiler for module {module_name} is {compiler_name}({compiler_path})",
            createdModule.Name ?? createdModule.GetType().Name, compiler.GetName(),
            compiler.GetExecutablePath());
        compiler.SetModule(createdModule);
        var targetWorkingDir = Path.GetFullPath(createdModule.OutputDirectory, moduleFile.Directory);
        Directory.CreateDirectory(targetWorkingDir);
        Directory.SetCurrentDirectory(targetWorkingDir);
        if (instancingParams.AdditionalCompilerOptions != null)
            compiler.AdditionalCompilerOptions.AddRange(instancingParams.AdditionalCompilerOptions!);
        if (instancingParams.AdditionalLinkerOptions != null)
            compiler.AdditionalLinkerOptions.AddRange(instancingParams.AdditionalLinkerOptions!);
        return compiler;
    }

    private async Task<CompilerBase> Create(string name)
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

    private Task<CompilerBase> Create(Type type)
    {
        return Create(GetNameOfCompiler(type));
    }

    public Task<T>? Create<T>() where T : CompilerBase
    {
        return Create(typeof(T)) as Task<T>;
    }


    private string GetNameOfCompiler(Type compilerType)
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


    private void Register(Type compilerType)
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