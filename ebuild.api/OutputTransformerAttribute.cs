using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    /// <summary>
    /// Marks a method as an output transformer for a module. Output transformers are
    /// callable hooks that change how a module's outputs are produced or arranged. Each
    /// transformer has a human readable <see cref="Name"/> and an identifier <see cref="Id"/>
    /// which is used in module references and the <c>RequestedOutput</c> context field.
    /// </summary>
    /// <remarks>
    /// Apply this attribute to an instance method on a <see cref="ModuleBase"/> derived class.
    /// The method will be discoverable via <see cref="ModuleBase.GetOutputTransformers"/> and
    /// can be invoked through the returned <see cref="MethodInvoker"/>.
    ///
    /// Example:
    /// <code>
    /// [OutputTransformer("packed", "packed")]
    /// private void PackedOutput() {
    ///     // rearrange output layout
    /// }
    /// </code>
    /// The optional <paramref name="id"/> must match the regex <c>[A-Za-z0-9+_\-.]+</c>.
    /// If <paramref name="id"/> is null the attribute will attempt to use a validated form of <paramref name="name"/> instead.
    /// </remarks>
    public partial class OutputTransformerAttribute : Attribute
    {
        /// <summary>
        /// Create a new OutputTransformerAttribute.
        /// </summary>
        /// <param name="name">Human-readable name shown in UIs and logs.</param>
        /// <param name="id">Optional short identifier used for selection (must match <c>[A-Za-z0-9+_\-.]+</c>).</param>
        /// <exception cref="ArgumentException">Thrown when neither <paramref name="id"/> nor <paramref name="name"/> are valid identifiers.</exception>
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

        /// <summary>
        /// Human-readable name for the output transformer.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Short identifier for the transformer. This value is used when requesting a specific
        /// output (for example via the module reference or the <c>RequestedOutput</c> context value).
        /// </summary>
        public string Id { get; private set; }

        private readonly Regex _idRegex = IdRegexGenerated();

        /// <summary>
        /// Generated regex that enforces valid identifier characters for the transformer id.
        /// Allowed characters: ASCII letters, digits, plus (+), underscore (_), hyphen (-), and dot (.).
        /// </summary>
        [GeneratedRegex(@"[A-Za-z0-9+_\-.]+")]
        private static partial Regex IdRegexGenerated();
    }
}