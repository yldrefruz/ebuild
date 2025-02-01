using System.Reflection;
using ebuild.api;
using BindingFlags = System.Reflection.BindingFlags;

namespace ebuild.Compilers;

public class CompilerRegistry
{
    public class ConstructorNotFoundException : Exception
    {
        private string _name;
        private ModuleBase _forModule;
        private ModuleContext _forModuleContext;
        private Type _compilerType;

        public ConstructorNotFoundException(Type compilerType, string name, ModuleBase module,
            ModuleContext moduleContext) : base(
            $"Compiler {name}'s constructor `{compilerType.Name}(ModuleBase, ModuleContext)` not found.")
        {
            _name = name;
            _compilerType = compilerType;
            _forModule = module;
            _forModuleContext = moduleContext;
        }
    }

    public class CompilerNotFoundException : Exception
    {
        private string _name;
        private ModuleBase _module;
        private ModuleContext _moduleContext;

        public CompilerNotFoundException(string name, ModuleBase module, ModuleContext moduleContext) : base(
            $"Compiler {name} is not found.")
        {
            _name = name;
            _module = module;
            _moduleContext = moduleContext;
        }
    }


    public class CompilerAttributeNotFoundException : Exception
    {
        private Type _type;

        public CompilerAttributeNotFoundException(Type type) : base($"{type} doesn't have Compiler attribute")
        {
            _type = type;
        }
    }

    private static readonly CompilerRegistry Instance = new();

    public static CompilerRegistry GetInstance() => Instance;

    CompilerBase Create(string name, ModuleBase module, ModuleContext moduleContext)
    {
        if (!_compilerTypeList.ContainsKey(name))
        {
            throw new CompilerNotFoundException(name, module, moduleContext);
        }
        var compilerType = _compilerTypeList[name];
        var constructorInfo =
            compilerType.GetConstructor(BindingFlags.Public, new[] { typeof(ModuleBase), typeof(ModuleContext) });
        if (constructorInfo == null)
            throw new ConstructorNotFoundException(compilerType, name, module, moduleContext);

        var created = constructorInfo.Invoke(new object?[] { module, moduleContext });
        return (CompilerBase)created;
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

    private readonly Dictionary<string, Type> _compilerTypeList = new();
}