using ebuild.api;

namespace ebuild.Platforms;

public class Win32Platform : PlatformBase
{
    public Win32Platform() : base("windows")
    {
    }
    public override string? GetDefaultToolchainName()
    {
        return "msvc";
    }
}