using System.Runtime.InteropServices;
using ebuild.api;
using ebuild.api.Compiler;
using ebuild.api.Toolchain;
using ebuild.Linkers;
using ebuild.Platforms;

namespace ebuild.Compilers;


public class NullCompilerFactory : ICompilerFactory
{
    public string Name => "null";

    public Type CompilerType => typeof(NullCompiler);

    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return true;
    }

    public CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new NullCompiler(module, instancingParams);
    }
}

public class NullCompiler(ModuleBase module, IModuleInstancingParams instancingParams) : CompilerBase(module, instancingParams)
{
    private static NullCompiler? _compiler;

    private class EmptyModule(ModuleContext context) : ModuleBase(context)
    {
    }

    public static NullCompiler Get()
    {
        if (_compiler != null) return _compiler;
        var nullFile = "/dev/null";
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            nullFile = "NUL:";
        }
        var modRef = new ModuleReference(outputType: "null",
                path: nullFile,
                version: "0",
                options: []
                );
        var module = new EmptyModule(new ModuleContext
        (
            reference: modRef,
            platform: PlatformRegistry.GetHostPlatform(),
            toolchain: IToolchainRegistry.Get().GetToolchain("null")!
        ));
        _compiler = new NullCompiler(module, new ModuleInstancingParams()
        {
            SelfModuleReference = modRef,
            Configuration = "Debug",
            Toolchain = IToolchainRegistry.Get().GetToolchain("null")!,
            Architecture = RuntimeInformation.ProcessArchitecture,
            Platform = PlatformRegistry.GetHostPlatform(),
            Options = [],
            AdditionalCompilerOptions = [],
            AdditionalLinkerOptions = [],
            AdditionalDependencyPaths = []
        });
        _compiler.SetModule(module);
        return _compiler;
    }

    public override bool IsAvailable(PlatformBase platform)
    {
        return true;
    }

    public override List<ModuleBase> FindCircularDependencies()
    {
        return [];
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