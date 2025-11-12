namespace thirdparty.icu;

using ebuild.api;



public class IcuPkgData : ModuleBase
{
    public IcuPkgData(ModuleContext context) : base(context)
    {
        this.Type = ModuleType.Executable;
        this.Name = "pkgdata";
        this.OutputDirectory = "Binaries/pkgdata";
        this.UseVariants = false;
        this.CppStandard = CppStandards.Cpp17;
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
        Includes.Private.Add("source/icu/source/common");
        Includes.Private.Add("source/icu/source/tools/toolutil");
        Includes.Private.Add("source/icu/source/tools/pkgdata");


        Dependencies.Private.Add("static:icu-toolutil");
        Dependencies.Private.Add("static:icu-i18n");
        Dependencies.Private.Add("static:icu-common");
        Dependencies.Private.Add("static:icu-stubdata");

        var sourceBase = Path.Join(context.ModuleDirectory.FullName, "source", "icu", "source", "tools", "pkgdata");

        // // patch source to allow compiling with a cpp compiler.
        var pkgtypes_cContent = File.ReadAllLines(Path.Join(sourceBase, "pkgtypes.c"));
        // patch: compilation errors
        pkgtypes_cContent[140] = "  newList = reinterpret_cast<CharList*>(uprv_malloc(sizeof(CharList)));";
        pkgtypes_cContent[214] = "    rPtr = const_cast<char*>(uprv_strrchr(strAlias, U_FILE_SEP_CHAR));";
        pkgtypes_cContent[217] = "        char *aPtr = const_cast<char*>(uprv_strrchr(strAlias, U_FILE_ALT_SEP_CHAR));";
        File.WriteAllLines(Path.Join(sourceBase, "pkgtypes.c"), pkgtypes_cContent);

        var pkgdata_cppContent = File.ReadAllLines(Path.Join(sourceBase, "pkgdata.cpp"));
        // patch: remove cdecl
        pkgdata_cppContent[57] = string.Empty;
        pkgdata_cppContent[59] = string.Empty;
        File.WriteAllLines(Path.Join(sourceBase, "pkgdata.cpp"), pkgdata_cppContent);
        // end patching
        var icuPkgDataSources = File.ReadAllLines(Path.Join(sourceBase, "sources.txt"));
        SourceFiles.AddRange(icuPkgDataSources.Select(line => Path.Join(sourceBase, line.Trim())).Where(File.Exists));
    }
}