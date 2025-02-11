using ebuild.api;

namespace ebuild.Platforms;

[Platform("Win32")]
public class Win32Platform : PlatformBase
{
    public override string? GetDefaultCompilerName()
    {
        //TODO: Load from ebuild.ini
        return "Msvc";
    }
}