using ebuild.api;

namespace ebuild.Platforms
{
    public class UnixPlatform : PlatformBase
    {
        public UnixPlatform() : base("unix")
        {
        }

        public override string ExtensionForStaticLibrary => ".a";

        public override string ExtensionForSharedLibrary => ".so";

        public override string ExtensionForExecutable => "";

        public override string? GetDefaultToolchainName()
        {
            return "gcc";
        }
    }
}