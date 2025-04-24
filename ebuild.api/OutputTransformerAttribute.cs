using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public partial class OutputTransformerAttribute : Attribute
{
    public OutputTransformerAttribute(string name, string? id)
    {
        Name = name;
        if (id != null && _idRegex.IsMatch(id))
        {
            Id = id;
        }
        else if (_idRegex.IsMatch(name))
        {
            Id = name;
        }
        else
        {
            throw new ArgumentException($"Invalid id for {name}. {id} doesn't match the regex {_idRegex}");
        }
    }

    public string Name { get; private set; }
    public string Id { get; private set; }

    private readonly Regex _idRegex = IdRegexGenerated();

    [GeneratedRegex(@"[A-Za-z0-9+_\-.]+")]
    private static partial Regex IdRegexGenerated();
}