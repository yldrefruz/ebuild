using ebuild.api;
using ebuild.Linkers;
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

        var module = new EmptyModule(new ModuleContext
        (
            reference: new ModuleReference(outputType: "null",
                path: nullFile,
                version: "0",
                options: new Dictionary<string, string>()),
            platform: PlatformRegistry.GetHostPlatform().GetName(),
            compiler: CompilerRegistry.GetDefaultCompilerName()
        ));
        _compiler = new NullCompiler();
        _compiler.SetModule(module);
        return _compiler;
    }

    public override LinkerBase GetDefaultLinker()
    {
        return LinkerRegistry.GetInstance().Get<NullLinker>();
    }

    public override bool IsAvailable(PlatformBase platform)
    {
        return true;
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        return new List<ModuleBase>();
    }

    public override Task<bool> Generate(string type, object? data = null)
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