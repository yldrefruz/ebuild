namespace ebuild.api;

public class AccessLimitList<T>
{
    public readonly List<T> Public = new();
    public readonly List<T> Private = new();

    public List<T> GetLimited(AccessLimit? limit)
    {
        if (limit != null)
        {
            switch (limit)
            {
                default:
                case AccessLimit.Private:
                    return Private;
                case AccessLimit.Public:
                    return Public;
            }
        }
        else
        {
            return Joined();
        }
    }

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


    public List<T> Joined()
    {
        List<T> all = new(Public);
        all.AddRange(Private);
        return all;
    }

    public void AddRange(AccessLimit limit, IEnumerable<T> enumerable)
    {
        GetLimited(limit).AddRange(enumerable);
    }
}