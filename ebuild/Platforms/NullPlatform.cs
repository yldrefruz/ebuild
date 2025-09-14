using System;
using System.Diagnostics.CodeAnalysis;
using ebuild.api;

namespace ebuild.Platforms;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class NullPlatform : PlatformBase
{
    public NullPlatform() : base("null")
    {
    }
    private static NullPlatform? _platform;

    public override string? GetDefaultToolchainName()
    {
        return "null";
    }

    public static PlatformBase Get()
    {
        return _platform ??= new NullPlatform();
    }
}