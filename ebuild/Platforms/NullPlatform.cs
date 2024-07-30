using System;

namespace ebuild.Platforms;

public class NullPlatform : Platform
{
    private static NullPlatform? _platform;

    public override Compiler? GetDefaultCompiler()
    {
        throw new NotImplementedException();
    }

    public override string GetName()
    {
        return "NullPlatform";
    }

    public static Platform Get()
    {
        return _platform ??= new NullPlatform();
    }
}