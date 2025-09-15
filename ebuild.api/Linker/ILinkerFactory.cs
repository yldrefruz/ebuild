namespace ebuild.api.Linker
{


    public interface ILinkerFactory
    {
        string Name { get; }
        Type LinkerType { get; }
        LinkerBase CreateLinker(ModuleBase module, IModuleInstancingParams moduleInstancingParams);
        bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams);

    }
}