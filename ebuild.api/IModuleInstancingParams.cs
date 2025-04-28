using System.Runtime.InteropServices;

namespace ebuild.api;

public interface IModuleInstancingParams
{
    public IModuleInstancingParams CreateCopyFor(ModuleReference targetModuleReference);
    public ModuleReference GetSelfModuleReference();
    public string GetConfiguration();
    public string GetCompilerName();
    public Architecture GetArchitecture();
    public string GetPlatformName();
    public Dictionary<string, string>? GetOptions();
    public List<string>? GetAdditionalCompilerOptions();
    public List<string>? GetAdditonalLinkerOptions();
    public List<string>? GetAdditionalDependencyPaths();

    public ModuleContext ToModuleContext();
}