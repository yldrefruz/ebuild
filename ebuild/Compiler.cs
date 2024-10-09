namespace ebuild;

public abstract class Compiler
{
    public abstract string GetName();
    public abstract string GetExecutablePath();

    private Module? _currentTarget;
    private bool _debugBuild;
    public List<string> AdditionalFlags = new();

    public void SetCurrentTarget(Module target)
    {
        _currentTarget = target;
    }

    public Module? GetCurrentTarget() => _currentTarget;
    public bool IsDebugBuild() => _debugBuild;

    public void SetDebugBuild(bool value)
    {
        _debugBuild = value;
    }

    public abstract void Compile(ModuleContext moduleContext);

    public virtual void Generate(string what, ModuleContext moduleContext)
    {
        return;
    }
}