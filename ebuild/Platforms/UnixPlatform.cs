using ebuild.api;

namespace ebuild.Platforms;

[Platform("Unix")]
public class UnixPlatform : PlatformBase
{
    public override string? GetDefaultCompilerName()
    {
        return "Gcc";
    }
}