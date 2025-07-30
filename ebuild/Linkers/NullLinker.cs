using ebuild.api;

namespace ebuild.Linkers;

[Linker("Null")]
public class NullLinker : LinkerBase
{
    public override bool IsAvailable(PlatformBase platform)
    {
        return true;
    }

    public override Task<bool> Setup()
    {
        return Task.FromResult(true);
    }

    public override Task<bool> Link()
    {
        return Task.FromResult(true);
    }

    public override string GetExecutablePath()
    {
        return "";
    }
}