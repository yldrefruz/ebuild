using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public abstract class PlatformBase
{
    private readonly string _name;

    protected PlatformBase()
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
        public readonly Type ForType;

        public NoPlatformAttributeException(Type forType) : base(
            $"{forType.Name} doesn't have the `Platform` attribute."
        )
        {
            ForType = forType;
        }
    }

    public string GetName()
    {
        return _name;
    }

    public abstract string? GetDefaultCompilerName();
}