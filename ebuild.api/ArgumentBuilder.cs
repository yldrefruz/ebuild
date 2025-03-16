namespace ebuild.api;

public class ArgumentBuilder
{
    public ArgumentBuilder Add(string command)
    {
        _args.Add(command);
        return this;
    }

    public ArgumentBuilder AddRange(IEnumerable<string> commands)
    {
        _args.AddRange(commands);
        return this;
    }

    private readonly List<string> _args = new();

    public override string ToString()
    {
        var str = string.Empty;
        foreach (var argument in _args)
        {
            var mutable = argument.Trim();
            char? inQuoteType = null;
            var requiresQuotes = false;
            foreach (var c in mutable)
            {
                if (char.IsWhiteSpace(c) && inQuoteType == null)
                {
                    requiresQuotes = true;
                    break;
                }

                if (inQuoteType == null && c is '"' or '\'')
                {
                    inQuoteType = c;
                    continue;
                }

                if (c != inQuoteType) continue;
                inQuoteType = null;
            }

            if (requiresQuotes)
            {
                mutable = '"' + mutable + '"';
            }

            if (!string.IsNullOrEmpty(str)) str += " ";
            str += mutable;
        }

        return str;
    }


    public static ArgumentBuilder operator +(ArgumentBuilder a, string other) => a.Add(other);
    public static ArgumentBuilder operator +(ArgumentBuilder a, IEnumerable<string> other) => a.AddRange(other);
}