namespace thirdparty.icu;





using ebuild.api;



public class IcuStubData : ModuleBase
{
    public IcuStubData(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.StaticLibrary;
        this.Name = "icustubdata";
        this.OutputFileName = "icudt77";
        this.OutputDirectory = "Binaries/icustubdata";
        this.UseVariants = false;
        this.CppStandard = CppStandards.Cpp17;
        Includes.Private.Add("source/icu/source/common");

        var sourceBase = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "stubdata");

        if (context.Platform.Name == "windows")
        {
            Definitions.Private.Add("_CRT_SECURE_NO_WARNINGS");
            Definitions.Private.Add("_CRT_SECURE_NO_DEPRECATE");
        }
        var icuStubDataSources = File.ReadAllLines(Path.Join(sourceBase, "sources.txt"));
        SourceFiles.AddRange(icuStubDataSources.Select(line => Path.Join(sourceBase, line.Trim())).Where(File.Exists));


        if (context.SelfReference.GetOutput() == "default" || string.IsNullOrEmpty(context.SelfReference.GetOutput()))
            throw new Exception("IcuStubData module must be built as a static or shared library. Please specify output type. Ex: static:icu-stubdata or shared:icu-stubdata");
    }


    [OutputTransformer("static", "static")]
    void TransformToStaticLibrary()
    {
        this.Type = ModuleType.StaticLibrary;
        Definitions.Public.Add("U_STATIC_IMPLEMENTATION=1");
    }
    [OutputTransformer("shared", "shared")]
    void TransformToSharedLibrary()
    {
        this.Type = ModuleType.SharedLibrary;
        Dependencies.Private.Add("shared:icu-common");
    }
}