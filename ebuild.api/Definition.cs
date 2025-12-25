using System.Diagnostics.CodeAnalysis;

namespace ebuild.api
{
    /// <summary>
    /// Represents a single preprocessor/definition entry typically specified as
    /// a string in the form <c>NAME=VALUE</c> or simply <c>NAME</c>.
    ///
    /// Behavior when the value is omitted: if the input string does not contain an
    /// equals sign ('='), the instance is treated as a definition with an implicit
    /// value of <c>"1"</c>. In that case <see cref="HasValue"/> returns <c>false</c>,
    /// <see cref="GetName"/> returns the entire input string, and <see cref="GetValue"/>
    /// returns <c>"1"</c>.
    /// </summary>
    /// <param name="inValue">The raw definition string passed to the constructor.
    /// Expected formats: <c>NAME=VALUE</c> or <c>NAME</c>.</param>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public class Definition(string inValue)
    {
        /// <summary>
        /// Gets the name (left-hand side) of the definition. If the original input
        /// contained an equals sign this returns the substring before the first '='.
        /// If no equals sign is present the whole input is returned as the name.
        /// </summary>
        /// <returns>The definition name.</returns>
        public string GetName() => HasValue() ? inValue.Split("=")[0] : inValue;

        /// <summary>
        /// Indicates whether the original input contained an explicit value part (an equals sign).
        /// </summary>
        /// <returns><c>true</c> if the input contains an '=' and therefore an explicit value; otherwise <c>false</c>.</returns>
        public bool HasValue() => inValue.Contains('=');

        /// <summary>
        /// Returns the value portion (right-hand side) of the definition.
        /// If no explicit value was provided the method returns the implicit default value <c>"1"</c>.
        /// </summary>
        /// <returns>The value string for the definition, or <c>"1"</c> when omitted.</returns>
        public string GetValue() => HasValue() ? inValue.Split("=")[1] : "1";

        /// <summary>
        /// Formats the definition as a string. If an explicit value was present the
        /// result is <c>NAME=VALUE</c>, otherwise it is just <c>NAME</c>.
        /// </summary>
        /// <returns>A string representation of the definition.</returns>
        public override string ToString()
        {
            var s = GetName();
            if (HasValue()) s += $"={GetValue()}";
            return s;
        }

        /// <summary>
        /// Implicit conversion from <see cref="string"/> to <see cref="Definition"/>,
        /// allowing convenient assignment from string literals like <c>"FOO=bar"</c> or <c>"BAR"</c>.
        /// </summary>
        /// <param name="s">The raw definition string.</param>
        public static implicit operator Definition(string s) => new(s);
    }
}