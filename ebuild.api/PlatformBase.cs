using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public abstract class PlatformBase(string name)
{
    public readonly string Name = name;

    public abstract string? GetDefaultToolchainName();
}