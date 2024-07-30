namespace ebuild.Platforms;

public class Win32Platform : Platform
{
    public override string GetName()
    {
        return "Win32Platform";
    }

    public override Compiler? GetDefaultCompiler()
    {
        return CompilerRegistry.GetCompilerByName("MSVC");
    }
}