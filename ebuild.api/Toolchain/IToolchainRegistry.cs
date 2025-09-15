using System.Reflection;

namespace ebuild.api.Toolchain
{



    public interface IToolchainRegistry
    {
        public static IToolchainRegistry Get() => EBuildInternals.Instance.GetService<IToolchainRegistry>() ?? throw new InvalidOperationException("IToolchainRegistry service not registered.");
        void RegisterToolchain(IToolchain toolchain);
        IToolchain? GetToolchain(string name);
        IToolchain? GetDefaultToolchain() => GetDefaultToolchainName() != null ? GetToolchain(GetDefaultToolchainName()!) : null;
        string? GetDefaultToolchainName();



        public void RegisterAllFromAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (typeof(IToolchain).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    if (Activator.CreateInstance(type) is IToolchain toolchain)
                    {
                        RegisterToolchain(toolchain);
                    }
                }
            }
        }


    }
}