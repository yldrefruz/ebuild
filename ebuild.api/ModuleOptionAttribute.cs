using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    /// <summary>
    /// Marks a field on a <see cref="ModuleBase"/> derived class as a configurable module option.
    /// Module options are discovered via reflection and converted from string values supplied by the
    /// module instancing parameters. This attribute controls option metadata such as the public name,
    /// whether the option is required, whether it affects the produced binary (variant id), and a default
    /// factory for missing values.
    /// </summary>
    /// <remarks>
    /// Apply the attribute to public or non-public fields inside your module class. Example:
    /// <code>
    /// [ModuleOption(Name = "enable_feature", Description = "Enable feature X", Required = false)]
    /// public bool EnableFeature = false;
    /// </code>
    /// When the module is instantiated the build system will use the option map provided in the
    /// <see cref="ModuleContext"/> (or <see cref="IModuleInstancingParams.Options"/>) to set the field value
    /// by converting from string to the field's type. If the field value is null the optional
    /// <see cref="Default"/> delegate will be invoked to obtain a fallback value.
    /// </remarks>
    public class ModuleOptionAttribute : Attribute
    {
        /// <summary>
        /// Regular expression used to validate option names. Valid names start with
        /// a letter, underscore, dash, plus, dollar, at-sign or dot and may contain
        /// letters, digits and the same punctuation afterwards.
        /// </summary>
        private static readonly Regex NameRegex = new(@"^[A-Za-z_\-+$@.]+[A-Za-z0-9_\-+$@.]*$", RegexOptions.Compiled);

        /// <summary>
        /// Optional explicit name for the module option. When not provided the field name
        /// is used as the option name.
        /// </summary>
        public string? Name;

        /// <summary>
        /// Optional description used for help or tooling UI.
        /// </summary>
        public string? Description;

        /// <summary>
        /// When <c>true</c> the option is considered to affect the produced binary and
        /// will be included when computing variant ids. Defaults to <c>true</c>.
        /// </summary>
        public bool ChangesResultBinary = true;

        /// <summary>
        /// When <c>true</c> the option must be provided; missing required options will
        /// cause the module instancing logic to emit an error message.
        /// </summary>
        public bool Required;

        /// <summary>
        /// Optional factory used to provide a default value when the field value is null.
        /// The delegate is invoked to obtain the default at read time.
        /// </summary>
        public Func<object?>? Default;

        private string? _cachedName;

        /// <summary>
        /// Returns the validated option name to use for this field. The returned value is cached
        /// after the first successful resolution.
        /// </summary>
        /// <param name="memberInfo">Reflection information for the field the attribute decorates.</param>
        /// <param name="validate">If <c>true</c>, validates the computed name against the allowed pattern and throws on failure.</param>
        /// <returns>The option name to use.</returns>
        public string GetName(FieldInfo memberInfo, bool validate = true)
        {
            if (_cachedName == null)
                _cachedName = GetName_Internal(memberInfo, validate);
            return _cachedName;
        }

        /// <summary>
        /// Internal implementation that resolves and (optionally) validates the option name.
        /// </summary>
        /// <param name="memberInfo">Field reflection information.</param>
        /// <param name="validate">If <c>true</c> enforce the name regex and throw an informative exception on failure.</param>
        /// <returns>Validated option name string.</returns>
        /// <exception cref="Exception">Thrown when the resolved name does not match the allowed pattern.</exception>
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

        /// <summary>
        /// Returns the effective value for the option on the given <paramref name="owner"/> instance.
        /// If the field value is null the optional <see cref="Default"/> delegate is invoked (if present)
        /// to obtain a fallback value. If neither a field value nor a default is available <c>null</c> is returned.
        /// </summary>
        /// <param name="owner">The object instance that owns the option field.</param>
        /// <param name="memberInfo">Reflection information for the field.</param>
        /// <returns>The effective option value or <c>null</c> when neither an explicit value nor a default exists.</returns>
        public object? GetValue(object owner, FieldInfo memberInfo)
        {
            return memberInfo.GetValue(owner) ?? (Default?.Invoke() ?? null);
        }
    }
}