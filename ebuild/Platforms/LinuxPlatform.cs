using ebuild.api;

namespace ebuild.Platforms;

[Platform("Linux")]
public class LinuxPlatform : PlatformBase
{
    public override string? GetDefaultCompilerName()
    {
        return "Gcc";
    }
}