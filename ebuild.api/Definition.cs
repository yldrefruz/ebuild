using System.Diagnostics.CodeAnalysis;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class Definition(string inValue)
    {
        public string GetName() => HasValue() ? inValue.Split("=")[0] : inValue;
        public bool HasValue() => inValue.Contains('=');
        public string GetValue() => HasValue() ? inValue.Split("=")[1] : "1";

        public override string ToString()
        {
            var s = GetName();
            if (HasValue()) s += $"={GetValue()}";
            return s;
        }

        public static implicit operator Definition(string s) => new(s);
    }
}