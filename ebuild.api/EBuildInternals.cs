using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;

namespace ebuild.api
{




    /// <summary>
    /// Provides a centralized, singleton-backed wrapper around a <see cref="System.ComponentModel.Design.ServiceContainer"/>
    /// for registering and resolving application services used by the EBuild system.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Instance"/> to access the single shared instance. This class delegates service storage and
    /// retrieval to an internal <see cref="System.ComponentModel.Design.ServiceContainer"/> instance.
    /// Callers are responsible for registering services during application initialization and ensuring that
    /// any registered services are safe for the intended concurrency model.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class EBuildInternals
    {

        /// <summary>
        /// Gets the singleton instance of <see cref="EBuildInternals"/> for registering and resolving services.
        /// </summary>
        /// <value>The single shared <see cref="EBuildInternals"/> instance.</value>
        public static EBuildInternals Instance { get; } = new();


        /// <summary>
        /// Retrieves a registered service of the specified generic type.
        /// </summary>
        /// <typeparam name="T">The service type to retrieve. Must be a reference type.</typeparam>
        /// <returns>The registered service instance of type <typeparamref name="T"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no service of the requested type is registered.</exception>
        /// <remarks>
        /// This method uses the underlying <see cref="System.ComponentModel.Design.ServiceContainer"/> to locate
        /// the service. It throws an <see cref="InvalidOperationException"/> if the service cannot be found,
        /// so callers that prefer a nullable result should call the container API directly.
        /// </remarks>
        public T GetService<T>() where T : class
        {
            return (T?)Services.GetService(typeof(T)) ?? throw new InvalidOperationException($"Service of type {typeof(T).FullName} not found.");
        }
        /// <summary>
        /// Registers a service instance for the specified generic type.
        /// </summary>
        /// <typeparam name="T">The service type to register. Must be a reference type.</typeparam>
        /// <param name="service">The service instance to register. Expected to be non-null.</param>
        /// <remarks>
        /// Registration is delegated to the underlying <see cref="System.ComponentModel.Design.ServiceContainer"/>.
        /// If a service for the same type is already registered, the behavior (e.g., replacement) is determined
        /// by the underlying container.
        /// </remarks>
        public void AddService<T>(T service) where T : class
        {
            Services.AddService(typeof(T), service);
        }
        /// <summary>
        /// Registers a service instance for the specified service type.
        /// </summary>
        /// <param name="serviceType">The <see cref="System.Type"/> under which to register the service. Cannot be null.</param>
        /// <param name="serviceInstance">The service instance to register. Expected to be non-null and compatible with <paramref name="serviceType"/>.</param>
        /// <remarks>
        /// This overload accepts a runtime <see cref="System.Type"/> and an object instance. Callers must ensure
        /// that <paramref name="serviceInstance"/> is assignable to <paramref name="serviceType"/>; otherwise,
        /// invalid runtime behavior may occur. Registration is delegated to the internal service container.
        /// </remarks>
        public void AddService(Type serviceType, object serviceInstance)
        {
            Services.AddService(serviceType, serviceInstance);
        }


        /// <summary>
        /// The underlying service container instance used to store and retrieve registered services.
        /// </summary>
        /// <remarks>
        /// This property is private to prevent external consumers from manipulating the container directly.
        /// It is an instance of <see cref="System.ComponentModel.Design.ServiceContainer"/> and is responsible
        /// for actual storage of service registrations.
        /// </remarks>
        private ServiceContainer Services { get; } = new();

        /// <summary>
        /// Event arguments for the <see cref="OnRegistryQuery"/> event.
        /// </summary>
        /// <param name="RegistryType">Type of the registry that started this query</param>
        /// <param name="RegistryInstance">Instance of the registry that started this query</param>
        public record OnRegistryQueryEventArgs(Type RegistryType, object? RegistryInstance)
        {

        }
        /// <summary>
        /// Event raised whenever a registry is querying something.
        /// </summary>
        /// <remarks>
        /// This event can be used to register custom types (e.g., module file generators, toolchains, etc.) into the registries
        /// </remarks>
        public static event EventHandler<OnRegistryQueryEventArgs>? OnRegistryQuery;


        /// <summary>
        /// Raises the <see cref="OnRegistryQuery"/> event.
        /// </summary>
        /// <param name="registryType">Type of the registry that is querying</param>
        /// <param name="registryInstance">Instance of the registry that is querying</param>
        public void RaiseOnRegistryQuery(Type registryType, object? registryInstance)
        {
            OnRegistryQuery?.Invoke(this, new OnRegistryQueryEventArgs(registryType, registryInstance));
        }
    }
}