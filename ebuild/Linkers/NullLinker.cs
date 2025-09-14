using ebuild.api;
using ebuild.api.Linker;

namespace ebuild.Linkers;

public class NullLinkerFactory : ILinkerFactory
{
    public string Name => "null";

    public Type LinkerType => typeof(NullLinker);

    public bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams) => true;

    public LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams instancingParams)
    {
        return new NullLinker(module, instancingParams);
    }
}

public class NullLinker(ModuleBase module, IModuleInstancingParams instancingParams) : LinkerBase(module, instancingParams)
{

    public override Task<bool> Setup()
    {
        return Task.FromResult(true);
    }

    public override Task<bool> Link()
    {
        return Task.FromResult(true);
    }

    public override string GetExecutablePath()
    {
        return "";
    }
}