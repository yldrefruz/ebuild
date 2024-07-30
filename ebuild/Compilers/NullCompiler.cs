namespace ebuild.Compilers;

public class NullCompiler : Compiler
{
    private static NullCompiler? _compiler;

    public static NullCompiler Get()
    {
        return _compiler ??= new NullCompiler();
    }

    public override string GetName()
    {
        return "NullCompiler";
    }

    public override string GetExecutablePath()
    {
        return "";
    }

    public override void Compile(ModuleContext context)
    {
        throw new NotImplementedException();
    }
}