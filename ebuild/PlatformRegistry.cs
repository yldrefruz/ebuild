using System.Reflection;
using ebuild.Platforms;

namespace ebuild;

public class PlatformRegistry
{
    private static List<Platform> _platforms = [];

    public static Platform GetNullPlatform()
    {
        return NullPlatform.Get();
    }

    public static void RegisterPlatform(Platform platform)
    {
        _platforms.Add(platform);
    }

    public static Platform? GetPlatformByName(string name)
    {
        return _platforms.FirstOrDefault(platform => platform.GetName() == name);
    }

    public static void LoadFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsSubclassOf(typeof(Platform))) continue;
            var createdPlatform = (Platform?)Activator.CreateInstance(type);
            if (createdPlatform == null) continue;
            RegisterPlatform(createdPlatform);
            Console.WriteLine("Registered platform {0}", createdPlatform.GetName());
        }
    }
}