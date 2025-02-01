using ebuild.api;
using ebuild.Platforms;

namespace ebuild.Compilers;

[Compiler("Null")]
public class NullCompiler : CompilerBase
{
    private static NullCompiler? _compiler;

    private class EmptyModule : ModuleBase
    {
        public EmptyModule(ModuleContext context) : base(context)
        {
        }
    }

    public static NullCompiler Get()
    {
        if (_compiler != null) return _compiler;
        var nullFile = "/dev/null";
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            nullFile = "NUL:";
        }

        var moduleContext = new ModuleContext(new FileInfo(nullFile), "NoBuild", PlatformRegistry.GetHostPlatform(),
            CompilerRegistry.GetInstance().GetNameOfCompiler<NullCompiler>(),
            new FileInfo(nullFile));
        var module = new EmptyModule(moduleContext);
        _compiler = new NullCompiler(module, moduleContext);
        return _compiler;
    }

    public NullCompiler(ModuleBase module, ModuleContext moduleContext) : base(module, moduleContext)
    {
    }

    public override bool IsAvailable(PlatformBase platform)
    {
        return true;
    }

    public override List<ModuleBase> HasCircularDependency()
    {
        return new List<ModuleBase>();
    }

    public override bool Generate(string type)
    {
        //The NullCompiler doesn't have any generate capability.
        return false;
    }

    public override Task<bool> Setup()
    {
        //Setup is empty
        return Task.FromResult(false);
    }

    public override Task<bool> Compile()
    {
        return Task.FromResult(false);
    }
}