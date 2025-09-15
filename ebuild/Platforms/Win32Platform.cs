using System.Runtime.InteropServices;
using ebuild.api;

namespace ebuild.Platforms
{
    public class Win32Platform : PlatformBase
    {
        public Win32Platform() : base("windows")
        {
        }

        public override string ExtensionForStaticLibrary => ".lib";

        public override string ExtensionForSharedLibrary => ".dll";

        public override string ExtensionForExecutable => ".exe";

        public override string? GetDefaultToolchainName()
        {
            return "msvc";
        }


        public override IEnumerable<string> GetPlatformLibrarySearchPaths(ModuleBase module)
        {
            // Here we add the Windows SDK library paths based on the target architecture. The other architectures are not supported, so user should provide them or the compilation will fail.
            if (module.Context.TargetArchitecture is not Architecture.X86 and not Architecture.X64 and not Architecture.Arm and not Architecture.Arm64)
            {
                yield break;
            }
            var searchPaths = new[] { "um", "ucrt", "ucrt_enclave" };
            var windowsKitInfo = MSVCUtils.GetWindowsKit(module.RequiredWindowsSdkVersion);
            if (windowsKitInfo != null)
            {
                foreach (var searchPath in searchPaths)
                {
                    var fullPath = Path.Join(windowsKitInfo.LibPath, searchPath,module.Context.TargetArchitecture switch
                    {
                        Architecture.X86 => "x86",
                        Architecture.X64 => "x64",
                        Architecture.Arm => "arm",
                        Architecture.Arm64 => "arm64",
                        _ => throw new NotSupportedException($"Architecture {module.Context.TargetArchitecture} is not supported on Msvc Cl.")
                    });
                    if (Directory.Exists(fullPath))
                    {
                        yield return fullPath;
                    }
                }
            }
            else
            {
                Console.Error.WriteLine("Warning: Couldn't find a valid Windows SDK installation. You may need to set the RequiredWindowsSdkVersion property on your module to a valid version.");
            }
        }

        public override IEnumerable<string> GetPlatformIncludes(ModuleBase module)
        {
            var includes = new[] { "ucrt", "um", "winrt", "shared" };
            var windowsKitInfo = MSVCUtils.GetWindowsKit(module.RequiredWindowsSdkVersion);
            if (windowsKitInfo != null)
            {
                foreach (var include in includes)
                {
                    var fullPath = Path.Join(windowsKitInfo.IncludePath, include);
                    if (Directory.Exists(fullPath))
                    {
                        yield return fullPath;
                    }
                }
            }
        }
    }
}