namespace ebuild.api;

public class Definition(string inValue)
{
    public string GetName() => HasValue() ? inValue.Split("=")[0] : inValue;
    public bool HasValue() => inValue.Contains('=');
    public string GetValue() => HasValue() ? inValue.Split("=")[1] : "1";
}