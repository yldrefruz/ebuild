using System.Runtime.InteropServices;

namespace ebuild;

public abstract class Platform
{
    public abstract string GetName();

    public abstract Compiler? GetDefaultCompiler();

    public static Platform GetHostPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformRegistry.GetPlatformByName("Win32Platform")!;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformRegistry.GetPlatformByName("LinuxPlatform")!;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PlatformRegistry.GetPlatformByName("OSXPlatform")!;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            return PlatformRegistry.GetPlatformByName("FreeBSDPlatform")!;
        return PlatformRegistry.GetNullPlatform();
    }
}