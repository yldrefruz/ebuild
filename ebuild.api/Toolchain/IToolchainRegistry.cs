using System.Reflection;

namespace ebuild.api.Toolchain
{



    public interface IToolchainRegistry
    {
        /// <summary>
        /// Returns the globally-registered <see cref="IToolchainRegistry"/> service instance
        /// from the internal service container. Throws when the service is not registered.
        /// </summary>
        public static IToolchainRegistry Get() => EBuildInternals.Instance.GetService<IToolchainRegistry>() ?? throw new InvalidOperationException("IToolchainRegistry service not registered.");

        /// <summary>
        /// Register a toolchain instance so it becomes discoverable by name.
        /// </summary>
        /// <param name="toolchain">The toolchain implementation to register.</param>
        void RegisterToolchain(IToolchain toolchain);

        /// <summary>
        /// Retrieve a registered toolchain by name.
        /// </summary>
        /// <param name="name">Name of the toolchain to retrieve.</param>
        /// <returns>The toolchain instance, or <c>null</c> if not found.</returns>
        IToolchain? GetToolchain(string name);

        /// <summary>
        /// Convenience helper to obtain the default toolchain instance, or <c>null</c>
        /// when no default is configured.
        /// </summary>
        /// <returns>The default <see cref="IToolchain"/>, or <c>null</c> if none.</returns>
        IToolchain? GetDefaultToolchain() => GetDefaultToolchainName() != null ? GetToolchain(GetDefaultToolchainName()!) : null;

        /// <summary>
        /// Returns the name of the default toolchain, or <c>null</c> when no default is set.
        /// </summary>
        /// <returns>Default toolchain name or <c>null</c>.</returns>
        string? GetDefaultToolchainName();


        /// <summary>
        /// Scans an assembly for concrete implementations of <see cref="IToolchain"/>,
        /// instantiates them and registers each via <see cref="RegisterToolchain"/>. This
        /// is a convenience helper for assembly-based registration.
        /// </summary>
        /// <param name="assembly">Assembly to scan for toolchain implementations.</param>
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