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

        var moduleContext = new ModuleContext(new FileInfo(nullFile), "no_build", PlatformRegistry.GetHostPlatform(),
            CompilerRegistry.GetInstance().GetNameOfCompiler<NullCompiler>(),
            new FileInfo(nullFile));
        var module = new EmptyModule(moduleContext);
        _compiler = new NullCompiler();
        _compiler.SetModule(module);
        return _compiler;
    }

    public NullCompiler() : base()
    {
    }

    public override bool IsAvailable(PlatformBase platform)
    {
        return true;
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        return new List<ModuleBase>();
    }

    public override Task<bool> Generate(string type)
    {
        //The NullCompiler doesn't have any generate capability.
        return Task.FromResult(false);
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

    public override string GetExecutablePath()
    {
        return "";
    }
}