namespace ebuild.api.Compiler;



public interface ICompilerFactory
{
    string Name { get; }
    Type CompilerType { get; }
    CompilerBase CreateCompiler(ModuleBase module, IModuleInstancingParams instancingParams);
    bool CanCreate(ModuleBase module, IModuleInstancingParams instancingParams);

}