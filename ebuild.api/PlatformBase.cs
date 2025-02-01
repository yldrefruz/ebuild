using System.Reflection;

namespace ebuild.api;

public abstract class PlatformBase
{
    private string _name;

    public PlatformBase()
    {
        if (GetType().GetCustomAttribute(typeof(PlatformAttribute)) is PlatformAttribute pa)
        {
            _name = pa.GetName();
        }
        else
        {
            throw new NoPlatformAttributeException(GetType());
        }
    }

    public class NoPlatformAttributeException : Exception
    {
        private Type _type;
        public NoPlatformAttributeException(Type type) : base(
            $"{type.Name} doesn't have the `Platform` attribute."
        )
        {
            _type = type;
        }
    }

    public string GetName()
    {
        return _name;
    }

    public abstract string? GetDefaultCompilerName();
}