using System.Reflection;
using ebuild.api;

namespace ebuild.Platforms;

public class PlatformRegistry
{
    public class PlatformNotFoundException : Exception
    {
        private string _name;

        public PlatformNotFoundException(string name) : base(
            $"{name} is not found."
        )
        {
            _name = name;
        }
    }

    public class ConstructorNotFoundException : Exception
    {
        private Type _type;

        public ConstructorNotFoundException(Type type) : base(
            $"Public constructor with no arguments not found for class {type.Name}.")
        {
            _type = type;
        }
    }

    public static PlatformRegistry GetInstance() => Instance;

    public static PlatformBase GetHostPlatform()
    {
        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32S:
            case PlatformID.Win32Windows:
            case PlatformID.Win32NT:
            case PlatformID.WinCE:
                return GetInstance().Get<Win32Platform>();
            case PlatformID.Unix:
                //TODO: Unix Platform
                //return GetInstance().Get<UnixPlatform>();
            case PlatformID.Xbox:
                //TODO: Xbox Platform
                //return GetInstance().Get<XboxPlatform>();
            case PlatformID.MacOSX:
                //TODO: Mac Platform
                //return GetInstance().Get<MacPlatform>();
            case PlatformID.Other:
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public PlatformBase Get<T>() where T : PlatformBase
    {
        if (typeof(T).GetCustomAttribute(typeof(PlatformAttribute)) is not PlatformAttribute pa)
            throw new PlatformBase.NoPlatformAttributeException(typeof(T));
        var name = pa.GetName();
        return Get(name);
    }

    public PlatformBase Get(string name)
    {
        if (!_platformList.TryGetValue(name, out PlatformBase? value))
            throw new PlatformNotFoundException(name);
        return value;
    }


    public void Register(PlatformBase platform)
    {
        _platformList.Add(platform.GetName(), platform);
    }

    public void Register(Type platformType)
    {
        if (!platformType.IsSubclassOf(typeof(PlatformBase)))
            throw new ArgumentException("platformType is not a subclass of PlatformBase");
        var constructor = platformType.GetConstructor(BindingFlags.Public, new Type[] { });
        if (constructor == null)
            throw new ConstructorNotFoundException(platformType);
        var platform = constructor.Invoke(null);
        _platformList.Add(((PlatformBase)platform).GetName(), (PlatformBase)platform);
    }

    public void Register<T>() where T : PlatformBase
    {
        Register(typeof(T));
    }

    private static readonly PlatformRegistry Instance = new();
    private readonly Dictionary<string, PlatformBase> _platformList = new();
}