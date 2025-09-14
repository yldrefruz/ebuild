using System.Reflection;
using ebuild.api;

namespace ebuild;



[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AutoRegisterServiceAttribute(Type serviceType) : Attribute
{
    public Type ServiceType { get; } = serviceType;



    public static void RegisterAllInAssembly(Assembly assembly)
    {
        var types = assembly.GetTypes().Where(t => t.GetCustomAttributes<AutoRegisterServiceAttribute>().Any());
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<AutoRegisterServiceAttribute>();
            if (attr != null)
            {
                var serviceType = attr.ServiceType;
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                {
                    EBuildInternals.Instance.AddService(serviceType, instance);
                }
            }
        }
    }
}