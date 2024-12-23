namespace ebuild.api;

public class AccessLimitList<T>
{
    public List<T> Public = new();
    public List<T> Private = new();

    public void Add(AccessLimit limit, T value)
    {
        switch (limit)
        {
            case AccessLimit.Public:
                Public.Add(value);
                break;
            default:
            case AccessLimit.Private:
                Private.Add(value);
                break;
        }
    }

    public void Add(T value)
    {
        Add(AccessLimit.Private, value);
    }
}