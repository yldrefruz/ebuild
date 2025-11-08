using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ebuild.api.Compiler;
using ebuild.api.Toolchain;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    /// <summary>
    /// Base class for all module definitions used by the build system.
    ///
    /// Module authors should derive from this class and configure the public fields
    /// (includes, source files, dependencies, options, etc.) in their module constructor.
    /// The build system reads these fields when constructing the build graph and invoking
    /// compilers/linkers.
    /// </summary>
    public abstract class ModuleBase
    {
        /// <summary>
        /// Per-access-limit preprocessor definitions for the C/C++ preprocessor.
        /// Use <see cref="Definitions.Public"/> for exported definitions and <see cref="Definitions.Private"/>
        /// for private-only definitions.
        /// </summary>
        public AccessLimitList<Definition> Definitions = new();

        /// <summary>
        /// Definitions used specifically for resource compilation (if the module uses resources).
        /// </summary>
        public AccessLimitList<Definition> ResourceDefinitions = new();

        /// <summary>
        /// Global definitions applied without access limits (applies to all translation units).
        /// </summary>
        public List<Definition> GlobalDefinitions = [];

        /// <summary>
        /// Public/private include directories for source compilation. Paths are interpreted
        /// relative to the module directory unless absolute paths are used.
        /// </summary>
        public AccessLimitList<string> Includes = new();

        /// <summary>
        /// Include directories specifically for resource compilation.
        /// </summary>
        public AccessLimitList<string> ResourceIncludes = new();

        /// <summary>
        /// Forced include directories. These directories are always added to the compiler
        /// invocation (for example via <c>-isystem</c> or equivalent).
        /// </summary>
        public AccessLimitList<string> ForceIncludes = new();

        /// <summary>
        /// Other ebuild modules that this module depends on. Access limits control whether a
        /// dependency is propagated transitively to dependents.
        /// </summary>
        public AccessLimitList<ModuleReference> Dependencies = new();

        /// <summary>
        /// Additional search paths that the resolver will consult when resolving dependencies.
        /// Useful for providing custom lookup directories for third-party libraries.
        /// </summary>
        public List<string> DependencySearchPaths = [];

        /// <summary>
        /// File-system dependencies to copy into the build output (for example data files or
        /// prebuilt libraries). These objects are copied into the build directory when the
        /// module is prepared.
        /// </summary>
        public AccessLimitList<AdditionalDependency> AdditionalDependencies = new();

        /// <summary>
        /// Libraries to link against. Use the access-limited lists to control transitive visibility.
        /// </summary>
        public AccessLimitList<string> Libraries = new();

        /// <summary>
        /// Libraries that should be delay-loaded (platform-specific behavior).
        /// </summary>
        public List<string> DelayLoadLibraries = [];

        /// <summary>
        /// Additional library search paths (absolute or relative to the module directory).
        /// </summary>
        public AccessLimitList<string> LibrarySearchPaths = new();

        /// <summary>
        /// Source file list for this module. Paths should point to the source files to compile.
        /// Use <see cref="ModuleUtilities.GetAllSourceFiles"/> for convenient collection patterns.
        /// </summary>
        public List<string> SourceFiles = [];

        /// <summary>
        /// Optional module name. When null, the module name is automatically derived from the
        /// module file name or the containing type name during construction.
        /// </summary>
        public string? Name;

        /// <summary>
        /// Output directory for built artifacts. This path is relative to the module directory
        /// unless an absolute path is provided. The final binary output path includes the
        /// output transformer name and variant id when applicable.
        /// </summary>
        public string OutputDirectory = "Binaries";

        /// <summary>
        /// Base name (file name without extension) for the produced output (library or executable).
        /// When null the <see cref="Name"/> is used as a default.
        /// </summary>
        public string? OutputFileName = null;

        /// <summary>
        /// Whether exception handling is enabled for compilation of this module.
        /// </summary>
        public bool EnableExceptions = false;

        /// <summary>
        /// Enables faster floating-point ABI/optimizations where supported by the compiler.
        /// </summary>
        public bool EnableFastFloatingPointOperations = true;

        /// <summary>
        /// Whether RTTI (typeid/dynamic_cast) is enabled for this module.
        /// </summary>
        public bool EnableRTTI = true;

        /// <summary>
        /// Controls whether debug information / separate debug files are produced. When null
        /// the build system chooses a sensible default based on configuration.
        /// </summary>
        public bool? EnableDebugFileCreation = null;

        /// <summary>
        /// Custom build steps to run before standard compile/link steps.
        /// </summary>
        public List<ModuleBuildStep> PreBuildSteps = [];

        /// <summary>
        /// Custom build steps to run after standard compile/link steps.
        /// </summary>
        public List<ModuleBuildStep> PostBuildSteps = [];

        /// <summary>
        /// Whether the build system should create per-variant directories (based on module options).
        /// When <c>false</c> the variant id is ignored and a single output directory is used.
        /// </summary>
        public bool UseVariants = true;

        /// <summary>
        /// CPU feature extensions requested by this module (for example SIMD flags).
        /// </summary>
        public CPUExtensions CPUExtension = CPUExtensions.Default;

        /// <summary>
        /// The C++ language standard requested for this module. The default is <see cref="CppStandards.Cpp20"/>.
        /// </summary>
        public CppStandards CppStandard = CppStandards.Cpp20;

        /// <summary>
        /// Optional C language standard for C modules. When set, this overrides <see cref="CppStandard"/>.
        /// </summary>
        public CStandards? CStandard = null;

        /// <summary>
        /// Optimization level used when compiling this module. Defaults to <see cref="OptimizationLevel.Speed"/>.
        /// </summary>
        public OptimizationLevel OptimizationLevel = OptimizationLevel.Speed;

        /// <summary>
        /// When set, indicates a minimum Windows SDK version required for building this module.
        /// </summary>
        public string? RequiredWindowsSdkVersion = null;

        /// <summary>
        /// The module's output type (executable, shared/static library, etc.).
        /// </summary>
        public ModuleType Type;

        /// <summary>
        /// Module runtime/context information provided by the caller when instancing modules.
        /// </summary>
        public ModuleContext Context;

        /// <summary>
        /// Additional raw compiler flags to pass to the compiler for this module.
        /// </summary>
        public List<string> CompilerOptions = [];

        /// <summary>
        /// Additional raw linker flags to pass to the linker for this module.
        /// </summary>
        public List<string> LinkerOptions = [];

        /// <summary>
        /// Base constructor used by derived module classes. Automatically applies option
        /// values from the provided <paramref name="context"/> and derives the module name
        /// when not explicitly set by the author.
        /// </summary>
        /// <param name="context">The module instancing context (paths, requested output, options, etc.).</param>
        protected ModuleBase(ModuleContext context)
        {
            Context = context;
            SetOptions(context.Options);
            Name ??= context.SelfReference is IModuleFile mf ? Path.GetFileNameWithoutExtension(mf.GetFilePath()) : GetType().Name;
        }

        /// <summary>
        /// Called after construction to perform common post-construction tasks:
        /// - Ensure OutputFileName has a sensible default.
        /// - Assign owners to additional dependencies.
        /// - Resolve dependency module paths.
        /// - Apply output transformer if a non-default transformer was requested.
        /// Finally calls <see cref="OnPostConstruction"/> for overrides.
        /// </summary>
        public void PostConstruction()
        {
            OutputFileName ??= string.IsNullOrEmpty(OutputFileName) ? Name : OutputFileName;
            AdditionalDependencies.Joined().ForEach(d => d.SetOwnerModule(this));
            Dependencies.Joined().ForEach(r => r.ResolveModulePath(this));
            if (!Context.RequestedOutput.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                var foundOutputTransformer = GetOutputTransformers().FirstOrDefault(tuple =>
                    tuple.Item2.Equals(Context.RequestedOutput, StringComparison.InvariantCultureIgnoreCase));
                foundOutputTransformer?.Item3.Invoke(this);
            }
            OnPostConstruction();
        }

        /// <summary>
        /// Override hook invoked at the end of <see cref="PostConstruction"/>. Derived classes
        /// may perform additional initialization here.
        /// </summary>
        public virtual void OnPostConstruction() { }

        /// <summary>
        /// Determines whether this module supports the given platform. Override in derived
        /// classes to restrict module availability on specific platforms.
        /// </summary>
        /// <param name="inPlatformBase">Target platform to check.</param>
        /// <returns><c>true</c> if the module can be built for the platform; otherwise <c>false</c>.</returns>
        public virtual bool IsPlatformSupported(PlatformBase inPlatformBase) => true;

        /// <summary>
        /// Determines whether the provided toolchain is supported by this module. Override to
        /// restrict compilers/toolchains.
        /// </summary>
        /// <param name="toolchain">Toolchain to check.</param>
        /// <returns><c>true</c> if supported; otherwise <c>false</c>.</returns>
        public virtual bool IsToolchainSupported(IToolchain toolchain) => true;

        /// <summary>
        /// Determines whether this module supports the given CPU architecture. Override to
        /// restrict support for particular architectures.
        /// </summary>
        /// <param name="architecture">Architecture to check.</param>
        /// <returns><c>true</c> if supported; otherwise <c>false</c>.</returns>
        public virtual bool IsArchitectureSupported(Architecture architecture) => true;


        /// <summary>
        /// Return a dictionary of module options (fields annotated with <see cref="ModuleOptionAttribute"/>).
        ///
        /// The returned dictionary maps option names to their current values. When <paramref name="onlyOutputChanging"/>
        /// is <c>true</c> only options which have <c>ChangesResultBinary=true</c> on their attribute are returned
        /// (used to compute variant ids).
        /// </summary>
        /// <param name="onlyOutputChanging">If <c>true</c>, return only options that affect the produced binary.</param>
        /// <returns>A dictionary mapping option names to their values.</returns>
        public Dictionary<string, object?> GetOptions(bool onlyOutputChanging = false)
        {
            Dictionary<string, object?> d = [];
            foreach (var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var attribute in field.GetCustomAttributes<ModuleOptionAttribute>(true))
                {
                    if (!onlyOutputChanging || attribute.ChangesResultBinary)
                        d.Add(attribute.GetName(field), attribute.GetValue(this, field));
                }
            }

            return d;
        }

        private string? _optionsString;

        /// <summary>
        /// Compute a stable string representing the selected options (option name/value pairs),
        /// used as input to variant id calculation. The result is cached for the lifetime of the instance.
        /// </summary>
        private string GetOptionsString(bool onlyOutputChanging)
        {
            if (_optionsString != null)
                return _optionsString;
            var strBuilder = new StringBuilder();
            var opts = GetOptions(onlyOutputChanging);
            opts.OrderBy(x => x.Key, StringComparer.InvariantCultureIgnoreCase).ToList().ForEach(x =>
            {
                if (strBuilder.Length > 0)
                    strBuilder.Append('\n');
                strBuilder.Append(x.Key).Append('=').Append(x.Value);
            });
            _optionsString = strBuilder.ToString();
            return _optionsString;
        }

        /// <summary>
        /// Compute a 32-bit variant id based on options that affect the produced binary. If
        /// <see cref="UseVariants"/> is false, the variant id will be zero.
        /// </summary>
        /// <returns>A variant id used to segregate build outputs.</returns>
        public uint GetVariantId()
        {
            if (!UseVariants)
                return 0;
            var optionsString = GetOptionsString(true);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(optionsString));
            return BitConverter.ToUInt32(hash, 0);
        }

        /// <summary>
        /// Apply option values from the provided string-based dictionary to fields on the module
        /// that are annotated with <see cref="ModuleOptionAttribute"/>. Conversion from string
        /// to the field type is attempted using TypeConverters. Conversion failures are logged
        /// to the module context as errors.
        /// </summary>
        /// <param name="options">String-keyed option map (name, string value).</param>
        private void SetOptions(Dictionary<string, string> options)
        {
            foreach (var field in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attr = field.GetCustomAttribute<ModuleOptionAttribute>();
                if (attr == null)
                    continue;
                var name = attr.GetName(field);
                if (!options.TryGetValue(name, out var value))
                {
                    if (attr.Required)
                    {
                        Context.AddMessage(ModuleContext.Message.SeverityTypes.Error,
                        $"Option {name}: Option is required but isn't supplied");
                    }
                    continue;
                }

                try
                {
                    var converter = TypeDescriptor.GetConverter(field.FieldType);
                    field.SetValue(this, converter.ConvertFromString(value!));
                }
                catch (Exception exception)
                {
                    Context.AddMessage(ModuleContext.Message.SeverityTypes.Error,
                        $"Option {name}: Couldn't apply value {value}. Conversion from string to {field.FieldType.FullName} failed.\n{exception.Message}");
                }
            }
        }

        /// <summary>
        /// Enumerates methods marked with <see cref="OutputTransformerAttribute"/>. Each returned tuple
        /// contains (name, id, invoker) where <c>invoker</c> can be used to invoke the transformer on this instance.
        /// Output transformers are used to change the module's output layout or artifacts.
        /// </summary>
        /// <returns>An enumerable of tuples: (name, id, method invoker).</returns>
        public IEnumerable<Tuple<string /*name*/, string /*id*/, MethodInvoker /*invoker for method*/>>
            GetOutputTransformers()
        {
            return from method in GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   let foundAttr = method.GetCustomAttribute<OutputTransformerAttribute>()
                   where foundAttr != null
                   select new Tuple<string, string, MethodInvoker>(foundAttr.Name, foundAttr.Id,
                       MethodInvoker.Create(method));
        }

        /// <summary>
        /// Returns the set of available output transformer (id, name) pairs.
        /// Useful for tooling to present selectable outputs to users.
        /// </summary>
        public HashSet<Tuple<string, string>> GetAvailableOutputIdAndNames()
        {
            HashSet<Tuple<string, string>> names = [];
            foreach (var transformer in GetOutputTransformers())
            {
                names.Add(new Tuple<string, string>(transformer.Item2, transformer.Item1));
            }

            return names;
        }

        /// <summary>
        /// Returns the human-readable name of the currently selected output transformer. If
        /// no transformer is selected the string "default" is returned.
        /// </summary>
        public string GetOutputTransformerName()
        {
            var foundOutputTransformer = GetOutputTransformers().FirstOrDefault(tuple =>
                tuple.Item2.Equals(Context.RequestedOutput, StringComparison.InvariantCultureIgnoreCase));
            if (foundOutputTransformer == null)
                return "default";
            return foundOutputTransformer.Item1;
        }

        /// <summary>
        /// Returns the absolute directory where this module's binary outputs will be written.
        /// The path includes the output transformer name and (when variants are enabled) the variant id.
        /// </summary>
        /// <returns>Absolute path to the module's binary output directory, always ending with a directory separator.</returns>
        public string GetBinaryOutputDirectory()
        {
            if (UseVariants)
                return Path.Combine(Context.ModuleDirectory!.FullName, OutputDirectory, GetOutputTransformerName(), GetVariantId().ToString()) + Path.DirectorySeparatorChar;
            return Path.Combine(Context.ModuleDirectory!.FullName, OutputDirectory, GetOutputTransformerName()) + Path.DirectorySeparatorChar;
        }



        /// <summary>
        /// Returns the full path to the produced binary file (executable or library) for the selected
        /// output transformer and variant. The file name is composed from <see cref="OutputFileName"/>
        /// and an extension chosen according to <see cref="Type"/> and <see cref="Context.Platform"/>.
        /// </summary>
        /// <returns>Absolute path to the output binary file.</returns>
        public string GetBinaryOutputPath()
        {
            return Path.Combine(GetBinaryOutputDirectory(), OutputFileName + Type switch
            {
                ModuleType.Executable => Context.Platform.ExtensionForExecutable,
                ModuleType.ExecutableWin32 => Context.Platform.ExtensionForExecutable,
                ModuleType.StaticLibrary => Context.Platform.ExtensionForStaticLibrary,
                ModuleType.SharedLibrary => Context.Platform.ExtensionForSharedLibrary,
                _ => throw new NotImplementedException($"Unknown module type {Type}"),
            });
        }
    }
}