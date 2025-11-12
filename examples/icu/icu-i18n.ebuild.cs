namespace thirdparty.icu;

using ebuild.api;





public class IcuI18N : ModuleBase
{
    public IcuI18N(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.SharedLibrary;
        this.Name = "icuin";
        this.OutputDirectory = "Binaries/icuin";
        this.UseVariants = false;
        this.CppStandard = CppStandards.Cpp17;
        Includes.Private.Add("source/icu/source/common");
        Includes.Private.Add("source/icu/source/i18n");

        var sourceBase = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "i18n");

        Definitions.Private.Add("U_I18N_IMPLEMENTATION=1");
        if (context.Platform.Name == "windows")
        {
            Definitions.Private.Add("_CRT_SECURE_NO_WARNINGS");
            Definitions.Private.Add("_CRT_SECURE_NO_DEPRECATE");
            CompilerOptions.Add("/sdl-");
        }
        var icuI18nSources = File.ReadAllLines(Path.Join(sourceBase, "sources.txt"));
        SourceFiles.AddRange(icuI18nSources.Select(line => Path.Join(sourceBase, line.Trim())).Where(File.Exists));

        if (context.SelfReference.GetOutput() == "default" || string.IsNullOrEmpty(context.SelfReference.GetOutput()))
            throw new Exception("IcuI18N module must be built as either static or shared library. Please specify output type. Ex: static:icu-18n or shared:icu-i18n");
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
        Dependencies.Private.Add("shared:icu-data");
    }
}