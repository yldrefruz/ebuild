using ebuild.api.Toolchain;
using ebuild.Platforms;

namespace ebuild.Toolchains
{


    [AutoRegisterService(typeof(IToolchainRegistry))]
    public class ToolchainRegistry : IToolchainRegistry
    {
        public IToolchain? GetToolchain(string name)
        {
            _toolchains.TryGetValue(name, out var toolchain);
            return toolchain;
        }

        public void RegisterToolchain(IToolchain toolchain)
        {
            _toolchains[toolchain.Name] = toolchain;
        }

        public string? GetDefaultToolchainName()
        {
            return PlatformRegistry.GetHostPlatform().GetDefaultToolchainName();
        }

        private readonly Dictionary<string, IToolchain> _toolchains = [];
    }
}