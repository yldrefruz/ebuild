using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ebuild.api.Toolchain;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    /// <summary>
    /// Runtime context provided to modules when they are instantiated.
    ///
    /// This class carries information about the calling environment such as the
    /// module reference, target platform, toolchain, architecture, and option map.
    /// Module implementations use this context during construction and initialization.
    /// </summary>
    public class ModuleContext
    {
        /// <summary>
        /// Copy constructor. Creates a shallow copy of the provided <paramref name="m"/> context.
        /// Lists and dictionaries are copied by reference (shallow copy) so callers should clone
        /// mutable collections when independent mutation is required.
        /// </summary>
        /// <param name="m">Existing <see cref="ModuleContext"/> to copy from.</param>
        public ModuleContext(ModuleContext m)
        {
            SelfReference = m.SelfReference;
            Platform = m.Platform;
            Toolchain = m.Toolchain;
            TargetArchitecture = m.TargetArchitecture;
            Options = m.Options;
            AdditionalDependencyPaths = m.AdditionalDependencyPaths;
        }

        /// <summary>
        /// Primary constructor used when instancing modules. At minimum a module reference,
        /// platform and toolchain must be provided.
        /// </summary>
        /// <param name="reference">Module identity (path, version, output, options).</param>
        /// <param name="platform">Target platform abstraction.</param>
        /// <param name="toolchain">Toolchain instance used for compilation/linking.</param>
        public ModuleContext(ModuleReference reference, PlatformBase platform, IToolchain toolchain)
        {
            SelfReference = reference;
            Platform = platform;
            Toolchain = toolchain;
        }

        /// <summary>
        /// The module reference (file path, output, version and options) that identifies the module being instantiated.
        /// </summary>
        public ModuleReference SelfReference;

        /// <summary>
        /// Target platform abstraction (provides platform-specific settings and file extensions).
        /// </summary>
        public PlatformBase Platform;

        /// <summary>
        /// The toolchain used to compile/link this module.
        /// </summary>
        public IToolchain Toolchain;

        /// <summary>
        /// Target CPU architecture. Defaults to the host OS architecture when not overridden.
        /// </summary>
        public Architecture TargetArchitecture = RuntimeInformation.OSArchitecture;

        /// <summary>
        /// Arbitrary key/value options passed to the module. Modules will typically read these
        /// to configure behavior. Defaults to an empty dictionary.
        /// </summary>
        public Dictionary<string, string> Options = [];

        /// <summary>
        /// Additional filesystem paths to search for dependencies (used by the resolver).
        /// </summary>
        public List<string> AdditionalDependencyPaths = [];

        /// <summary>
        /// Optional back-reference to the original instancing parameter object used to create this context.
        /// May be null when the context was created directly.
        /// </summary>
        public IModuleInstancingParams? InstancingParams;


        /// <summary>
        /// Convenience accessor for a <see cref="FileInfo"/> representing the module file identified by <see cref="SelfReference"/>.
        /// </summary>
        public FileInfo ModuleFile => new(SelfReference.GetFilePath());

        /// <summary>
        /// Directory that contains the module file. This property is never null for a valid module reference.
        /// </summary>
        public DirectoryInfo ModuleDirectory => ModuleFile.Directory!;

        /// <summary>
        /// Build configuration name (for example "debug" or "release"). Default is "debug".
        /// </summary>
        public string Configuration = "debug";

        /// <summary>
        /// Requested version string for the module as encoded in the <see cref="SelfReference"/>.
        /// </summary>
        public string RequestedVersion => SelfReference.GetVersion();

        /// <summary>
        /// Requested output transformer id for the module as encoded in the <see cref="SelfReference"/>.
        /// </summary>
        public string RequestedOutput => SelfReference.GetOutput();


        /// <summary>
        /// Simple message wrapper used to surface informational/warning/error messages produced
        /// during module instantiation.
        /// </summary>
        /// <param name="value">Text of the message.</param>
        /// <param name="type">Severity level of the message.</param>
        public class Message(string value, Message.SeverityTypes type)
        {
            /// <summary>
            /// Severity level for module messages.
            /// </summary>
            public enum SeverityTypes
            {
                /// <summary>Informational message with no build impact.</summary>
                Info,
                /// <summary>Warning that may indicate non-fatal issues.</summary>
                Warning,
                /// <summary>Error that typically prevents successful module instantiation.</summary>
                Error
            }

            /// <summary>
            /// Returns the message severity.
            /// </summary>
            public SeverityTypes GetSeverity() => type;

            /// <summary>
            /// Returns the message text.
            /// </summary>
            public string GetMessage() => value;

            /// <summary>
            /// Returns a compact representation in the form "Severity : Message".
            /// </summary>
            public override string ToString()
            {
                return type + " : " + value;
            }
        }

        /// <summary>
        /// Collected informational/warning/error messages produced while creating or configuring the module.
        /// </summary>
        public List<Message> Messages = [];


        /// <summary>
        /// Append a message with the specified <paramref name="severityType"/> and <paramref name="message"/> text.
        /// Module implementations and instancing code use this to report diagnostics to callers.
        /// </summary>
        /// <param name="severityType">Severity of the message (Info/Warning/Error).</param>
        /// <param name="message">Textual message to append.</param>
        public void AddMessage(Message.SeverityTypes severityType, string message)
        {
            Messages.Add(new Message(message, severityType));
        }
    }
}