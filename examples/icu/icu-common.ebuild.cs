namespace thirdparty.icu;

using ebuild.api;





public class IcuCommon : ModuleBase
{
    public IcuCommon(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.SharedLibrary;
        this.Name = "icuuc";
        this.OutputDirectory = "Binaries/icuuc";
        this.UseVariants = false;
        this.CppStandard = CppStandards.Cpp20;
        var sourceBase = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "common");

        Definitions.Private.Add("U_COMMON_IMPLEMENTATION=1");
        if (context.Platform.Name == "windows")
        {
            Definitions.Private.Add("_CRT_SECURE_NO_WARNINGS");
            Definitions.Private.Add("_CRT_SECURE_NO_DEPRECATE");
            CompilerOptions.Add("/sdl-");
        }
        var icuCommonSources = File.ReadAllLines(Path.Join(sourceBase, "sources.txt"));
        Includes.Private.Add("source/icu/source/common"); // Ensure the unicode/* headers resolves to this correctly.
        SourceFiles.AddRange(icuCommonSources.Select(line => Path.Join(sourceBase, line.Trim())).Where(File.Exists));
        if(context.Platform.Name == "windows")
        {
            Libraries.Private.Add("Advapi32.lib");
        }
        if (context.RequestedOutput is not "static" and not "shared")
        {
            throw new Exception("Invalid output type for IcuCommon module. Must be either static or shared.");
        }
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
        Dependencies.Private.Add("shared:icu-data");
    }
}