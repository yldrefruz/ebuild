using System.Diagnostics;
using System.Reflection;

namespace ebuild;

public class CompilerRegistry
{
    private static readonly List<Compiler> Compilers = new();

    public static void RegisterCompiler(Compiler compiler)
    {
        Compilers.Add(compiler);
    }

    public static Compiler? GetCompilerByName(string name)
    {
        return Compilers.Find(compiler => compiler.GetName() == name);
    }

    private static Compiler? GetCompilerFromEnv()
    {
        var environmentVariable = Environment.GetEnvironmentVariable("COMPILER");
        return environmentVariable != null ? GetCompilerByName(environmentVariable) : null;
    }

    private static Compiler? GetCompilerFromArgs()
    {
        var args = System.Environment.GetCommandLineArgs();
        string? compilerName = null;
        for (int i = 0; i < args.Length; ++i)
        {
            if (args[i].StartsWith("--compiler"))
            {
                if (args[i].StartsWith("--compiler="))
                {
                    var argKeyValue = args[i].Split("=");
                    if (argKeyValue.Length == 2)
                        compilerName = argKeyValue[1];
                }
                else if (i + 1 < args.Length)
                {
                    compilerName = args[i + i];
                }
            }
        }

        return compilerName == null ? null : GetCompilerByName(compilerName);
    }

    public static Compiler GetCompiler(Module module)
    {
        if (module.CompilerName != null)
        {
            Compiler? compiler = GetCompilerByName(module.CompilerName);
            if (compiler != null)
                return compiler;
            if (module.ForceNamedCompiler)
            {
                throw new Exception($"Invalid forced compiler name {module.CompilerName}.");
            }
        }

        var argsCompiler = GetCompilerFromArgs();
        if (argsCompiler != null)
            return argsCompiler;
        var envCompiler = GetCompilerFromEnv();
        if (envCompiler != null)
            return envCompiler;
        var platformCompiler = Platform.GetHostPlatform().GetDefaultCompiler();
        if (platformCompiler != null)
            return platformCompiler;
        throw new Exception("Can't find a valid compiler.");
    }

    public static void LoadFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(Compiler))) continue;
            var createdCompiler = (Compiler?)Activator.CreateInstance(type);
            if (createdCompiler == null) continue;
            Console.WriteLine("Registering compiler {0}", createdCompiler.GetName());
            RegisterCompiler(createdCompiler);
        }
    }
}