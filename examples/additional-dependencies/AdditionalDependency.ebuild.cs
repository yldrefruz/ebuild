using ebuild.api;


namespace ebuild.examples.additional_dependencies;




public class AdditionalDependencyExampleModule : ModuleBase
{
    public AdditionalDependencyExampleModule(ModuleContext context) : base(context)
    {
        Name = "AdditionalDependencyExampleModule";
        Type = ModuleType.Executable;
        SourceFiles.Add("main.c");
        CStandard = CStandards.C11;

        // Define an additional dependency to be copied.
        AdditionalDependencies.Private.Add(new AdditionalDependency("example.txt"));
    }
}