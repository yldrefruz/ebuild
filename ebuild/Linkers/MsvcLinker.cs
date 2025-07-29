using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ebuild.api;
using Microsoft.Extensions.Logging;

namespace ebuild.Linkers;

[Linker("Msvc")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class MsvcLinker : LinkerBase
{
    private MsvcLinkLinker? _linkLinker;
    private MsvcLibLinker? _libLinker;

    private static readonly ILogger Logger = EBuild.LoggerFactory.CreateLogger("MSVC Linker");

    public override bool IsAvailable(PlatformBase platform)
    {
        return platform.GetName() == "Win32";
    }

    public override async Task<bool> Setup()
    {
        _linkLinker = new MsvcLinkLinker();
        _libLinker = new MsvcLibLinker();
        
        var linkSetup = await _linkLinker.Setup();
        var libSetup = await _libLinker.Setup();
        
        return linkSetup && libSetup;
    }

    public override async Task<bool> Link()
    {
        if (CurrentModule == null)
        {
            Logger.LogError("No module set for linking");
            return false;
        }

        Logger.LogInformation("Linking module {moduleName}", CurrentModule.Name);

        try
        {
            LinkerBase? linker;
            switch (CurrentModule.Type)
            {
                case ModuleType.StaticLibrary:
                    linker = _libLinker;
                    break;
                case ModuleType.SharedLibrary:
                case ModuleType.Executable:
                case ModuleType.ExecutableWin32:
                    linker = _linkLinker;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (linker == null)
            {
                Logger.LogError("Failed to get appropriate linker for module type {moduleType}", CurrentModule.Type);
                return false;
            }

            // Set the module and additional options on the specialized linker
            linker.SetModule(CurrentModule);
            linker.AdditionalLinkerOptions.Clear();
            linker.AdditionalLinkerOptions.AddRange(AdditionalLinkerOptions);
            
            return await linker.Link();
        }
        catch (Exception ex)
        {
            Logger.LogError("Linking failed with exception: {message}", ex.Message);
            return false;
        }
    }

    public override string GetExecutablePath()
    {
        if (CurrentModule == null)
            throw new NullReferenceException("CurrentModule is null.");
            
        return CurrentModule.Type switch
        {
            ModuleType.StaticLibrary => _libLinker?.GetExecutablePath() ?? "lib.exe",
            ModuleType.SharedLibrary or ModuleType.Executable or ModuleType.ExecutableWin32 => _linkLinker?.GetExecutablePath() ?? "link.exe",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}