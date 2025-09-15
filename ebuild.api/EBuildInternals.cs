using System.ComponentModel.Design;

namespace ebuild.api
{




    public class EBuildInternals
    {
        public static EBuildInternals Instance { get; } = new();


        public T GetService<T>() where T : class
        {
            return (T?)Services.GetService(typeof(T)) ?? throw new InvalidOperationException($"Service of type {typeof(T).FullName} not found.");
        }

        public void AddService<T>(T service) where T : class
        {
            Services.AddService(typeof(T), service);
        }
        public void AddService(Type serviceType, object serviceInstance)
        {
            Services.AddService(serviceType, serviceInstance);
        }



        private ServiceContainer Services { get; } = new();
    }
}