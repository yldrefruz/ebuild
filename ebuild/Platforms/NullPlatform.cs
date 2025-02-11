using System;
using System.Diagnostics.CodeAnalysis;
using ebuild.api;

namespace ebuild.Platforms;

[Platform("Null")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class NullPlatform : PlatformBase
{
    private static NullPlatform? _platform;

    public override string? GetDefaultCompilerName()
    {
        return "Null";
    }

    public static PlatformBase Get()
    {
        return _platform ??= new NullPlatform();
    }
}