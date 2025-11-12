namespace thirdparty.icu;

using ebuild.api;


public class IcuPkg : ModuleBase
{
    public IcuPkg(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.Executable;
        this.Name = "icupkg";
        this.OutputDirectory = "Binaries/icupkg";
        this.UseVariants = false;
        this.CppStandard = CppStandards.Cpp17;
        var sourceBase = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "tools", "icupkg");

        Includes.Private.Add("source/icu/source/common");
        Includes.Private.Add("source/icu/source/tools/toolutil");

        // ordering of these is important
        Dependencies.Private.Add("static:icu-toolutil");
        Dependencies.Private.Add("static:icu-i18n");
        Dependencies.Private.Add("static:icu-common");
        Dependencies.Private.Add("static:icu-stubdata");




        Definitions.Private.Add("U_STATIC_IMPLEMENTATION");
        if (context.Platform.Name == "windows")
        {
            Definitions.Private.Add("_CRT_SECURE_NO_WARNINGS");
            Definitions.Private.Add("_CRT_SECURE_NO_DEPRECATE");
        }

        var icuPkgSources = File.ReadAllLines(Path.Join(sourceBase, "sources.txt"));
        SourceFiles.AddRange(icuPkgSources.Select(line => Path.Join(sourceBase, line.Trim())).Where(File.Exists));
    }
}