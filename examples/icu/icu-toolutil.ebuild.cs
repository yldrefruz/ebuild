namespace thirdparty.icu;

using ebuild.api;



public class IcuToolUtil : ModuleBase
{
    public IcuToolUtil(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.StaticLibrary;
        this.Name = "icutu";
        this.OutputDirectory = "Binaries/icutu";
        this.UseVariants = false;
        this.CppStandard = CppStandards.Cpp17;
        var sourceBase = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "tools", "toolutil");


        Includes.Private.Add("source/icu/source/common");
        Includes.Private.Add("source/icu/source/i18n");
        Includes.Private.Add("source/icu/source/tools/toolutil");

        Definitions.Private.Add("U_TOOLUTIL_IMPLEMENTATION=1");
        if (context.Platform.Name == "windows")
        {
            Definitions.Private.Add("_CRT_SECURE_NO_WARNINGS");
            Definitions.Private.Add("_CRT_SECURE_NO_DEPRECATE");
        }
        else if (context.Platform.Name == "unix")
        {
            //TODO: add real tests so the U_HAVE_ELF_H is correctly set.
            Definitions.Private.Add("U_HAVE_ELF_H=1");
            Definitions.Private.Add("U_PLATFORM_IS_LINUX_BASED=1");
        }
        var toolutilSources = File.ReadAllLines(Path.Join(sourceBase, "sources.txt"));
        SourceFiles.AddRange(toolutilSources.Select(line => Path.Join(sourceBase, line.Trim())).Where(File.Exists));


        if (context.SelfReference.GetOutput() == "default" || string.IsNullOrEmpty(context.SelfReference.GetOutput()))
            throw new Exception("IcuToolUtil module must be built as either static or shared library. Please specify output type. Ex: static:icu-toolutil or shared:icu-toolutil");
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
        Dependencies.Private.Add("shared:icu-i18n");
    }
}