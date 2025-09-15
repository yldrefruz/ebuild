using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ModuleOptionAttribute : Attribute
    {
        private static readonly Regex NameRegex = new(@"^[A-Za-z_\-+$@.]+[A-Za-z0-9_\-+$@.]*$", RegexOptions.Compiled);

        public string? Name;
        public string? Description;
        public bool ChangesResultBinary = true;
        public bool Required;
        public Func<object?>? Default;
        private string? _cachedName;

        public string GetName(FieldInfo memberInfo, bool validate = true)
        {
            if (_cachedName == null)
                _cachedName = GetName_Internal(memberInfo, validate);
            return _cachedName;
        }

        internal string GetName_Internal(FieldInfo memberInfo, bool validate = true)
        {
            var nCandidate = Name ?? memberInfo.Name;
            if (NameRegex.IsMatch(nCandidate))
                return nCandidate;
            List<int> nonMatchingParts = [];
            for (var i = 0; i < nCandidate.Length; ++i)
            {
                if (!NameRegex.Matches(nCandidate).AsEnumerable().Any(m => m.Index >= i && m.Index + m.Length <= i))
                {
                    //no match
                    nonMatchingParts.Add(i);
                }
            }

            StringBuilder nonMatchingBuilder = new(nCandidate.Length);
            for (var i = 0; i < nCandidate.Length; ++i)
            {
                if (nonMatchingParts.Contains(i))
                {
                    nonMatchingBuilder.Append('^');
                }
                else
                {
                    nonMatchingBuilder.Append(' ');
                }
            }

            throw new Exception(
                $"Module option name isn't valid. It should use the regex {NameRegex.ToString()}\n{nCandidate}\n{nonMatchingBuilder}");
        }

        public object? GetValue(object owner, FieldInfo memberInfo)
        {
            return memberInfo.GetValue(owner) ?? (Default?.Invoke() ?? null);
        }
    }
}