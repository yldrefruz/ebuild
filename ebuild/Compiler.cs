namespace ebuild;

public abstract class Compiler
{
    public abstract string GetName();
    public abstract string GetExecutablePath();
    
    private Module? _currentTarget;
    public void SetCurrentTarget(Module target)
    {
        _currentTarget = target;
    }
    public Module? GetCurrentTarget() => _currentTarget;

    public abstract void Compile(ModuleContext moduleContext);
}