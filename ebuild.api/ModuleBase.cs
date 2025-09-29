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
    public abstract class ModuleBase
    {
        /// <summary>The definitions to use.</summary>
        public AccessLimitList<Definition> Definitions = new();
        public AccessLimitList<Definition> ResourceDefinitions = new();
        public List<Definition> GlobalDefinitions = [];

        /// <summary>Include directories to use.</summary>
        public AccessLimitList<string> Includes = new();
        public AccessLimitList<string> ResourceIncludes = new();

        /// <summary> Forced include directories to use. </summary>
        public AccessLimitList<string> ForceIncludes = new();

        /// <summary>Other modules to depend on (ebuild modules.)</summary> 
        public AccessLimitList<ModuleReference> Dependencies = new();

        /// <summary>
        /// The search paths to use for dependencies.
        /// This is used to find the dependencies.
        /// </summary>
        public List<string> DependencySearchPaths = [];

        /// <summary>Dependencies to add for this module. These are copied to the build directory.</summary>
        public AccessLimitList<AdditionalDependency> AdditionalDependencies = new();

        /// <summary>The libraries to link.</summary>
        public AccessLimitList<string> Libraries = new();
        public List<string> DelayLoadLibraries = [];

        /// <summary>The library paths to search for. Absolute or relevant</summary>
        public AccessLimitList<string> LibrarySearchPaths = new();

        /// <summary>
        /// The source files to use. This is a list of files which are used to compile the module.
        /// </summary>
        public List<string> SourceFiles = [];

        /// <summary>
        /// The name of the module. If null will automatically deduce the name from the file name.
        /// </summary> 
        public string? Name;
        /// <summary>
        ///  The output directory for the module. This is relative to the build directory or absolute.
        ///  The variant directory will be inside this directory.
        /// </summary>
        public string OutputDirectory = "Binaries";
        /// <summary>
        /// Name of the output file without extension.
        /// </summary>
        public string? OutputFileName = null;
        public bool EnableExceptions = false;
        public bool EnableFastFloatingPointOperations = true;
        public bool EnableRTTI = true;
        public bool? EnableDebugFileCreation = null;

        public List<IModuleBuildStep> PreBuildSteps = [];
        public List<IModuleBuildStep> PostBuildSteps = [];

        /// <summary>
        /// Should we use variants for this module?
        /// </summary>
        public bool UseVariants = true;
        public CPUExtensions CPUExtension = CPUExtensions.Default;

        /// <summary>The cpp standard this module uses.</summary>
        public CppStandards CppStandard = CppStandards.Cpp20;

        /// <summary>The C standard this module uses. If set, CppStandard is ignored.</summary>
        public CStandards? CStandard = null;

        /// <summary>The optimization level for this module.</summary>
        public OptimizationLevel OptimizationLevel = OptimizationLevel.Speed;
        public string? RequiredWindowsSdkVersion = null;

        /// <summary> The type of this module</summary>
        public ModuleType Type;

        public ModuleContext Context;

        public List<string> CompilerOptions = [];
        public List<string> LinkerOptions = [];

        protected ModuleBase(ModuleContext context)
        {
            Context = context;
            SetOptions(context.Options);
            Name ??= context.SelfReference is IModuleFile mf ? Path.GetFileNameWithoutExtension(mf.GetFilePath()) : GetType().Name;
        }

        /// <summary>
        /// Called after the module is constructed.
        /// This is used for post construction tasks.
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

        }

        public virtual void OnPostConstruction() { }

        /// <summary>
        /// Checks if the module is supported on the current platform.
        /// This is used to check if the module can be compiled on the current platform.
        /// </summary>
        /// <param name="inPlatformBase">The platform to compile for</param>
        /// <returns>whether support is available</returns>
        public virtual bool IsPlatformSupported(PlatformBase inPlatformBase) => true;

        public virtual bool IsToolchainSupported(IToolchain toolchain) => true;

        /// <summary>
        /// Checks if the architecture is supported.
        /// This is used to check if the module can be compiled for the given architecture.
        /// </summary>
        /// <param name="architecture">The architecture to check for</param>
        /// <returns>true if support is available, false otherwise</returns>
        public virtual bool IsArchitectureSupported(Architecture architecture) => true;


        /// <summary>
        /// Gets the options for this module.
        /// The options are the fields which are marked with the ModuleOptionAttribute.
        /// </summary>
        /// <param name="onlyOutputChanging">If true returns only output changing options, otherwise returns all options.</param>
        /// <returns>the resulting option dictionary</returns>
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
        /// Gets the options string for this module.
        /// The options string is a string which contains all of the options for this module.
        /// It is used to generate the variant id.
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
        ///Gets the id of the variant.
        /// </summary>
        public uint GetVariantId()
        {
            if (!UseVariants)
                return 0;
            var optionsString = GetOptionsString(true);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(optionsString));
            return BitConverter.ToUInt32(hash, 0);
        }

        /// <summary>
        /// Sets the options for this module.
        /// </summary>
        /// <param name="options">the dictionary containing options to pass.</param>
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
        /// Gets all of the output transformers for this module.
        /// The output transformers are methods which are marked with the OutputTransformerAttribute.
        /// </summary>
        /// <returns>
        /// A list of tuples which contain the name, id and method invoker for the output transformers.
        /// </returns>
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
        /// Get a list of tuples which contain the id and name of the output transformers.
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
        /// Gets the current output transformer name
        /// If no transformer is passed, the result will be "default"
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
        /// Gets the output directory for the module. 
        /// The path is absolute and contains the variant id if variants are used.
        /// </summary>
        public string GetBinaryOutputDirectory()
        {
            if (UseVariants)
                return Path.Combine(Context.ModuleDirectory!.FullName, OutputDirectory, GetOutputTransformerName(), GetVariantId().ToString()) + Path.DirectorySeparatorChar;
            return Path.Combine(Context.ModuleDirectory!.FullName, OutputDirectory, GetOutputTransformerName()) + Path.DirectorySeparatorChar;
        }



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