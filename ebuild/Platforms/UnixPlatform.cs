using ebuild.api;

namespace ebuild.Platforms;

public class UnixPlatform : PlatformBase
{
    public UnixPlatform() : base("unix")
    {
    }
    public override string? GetDefaultToolchainName()
    {
        return "gcc";
    }
}