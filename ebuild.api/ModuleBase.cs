using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace ebuild.api;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public abstract class ModuleBase
{
    /// <summary>The definitions to use.</summary>
    public AccessLimitList<Definition> Definitions = new();

    /// <summary>Include directories to use.</summary>
    public AccessLimitList<string> Includes = new();

    /// <summary> Forced include directories to use. </summary>
    public AccessLimitList<string> ForceIncludes = new();
    
    /// <summary>Other modules to depend on (ebuild modules.)</summary> 
    public AccessLimitList<ModuleReference> Dependencies = new();

    public List<string> DependencySearchPaths = new();

    /// <summary>Dependencies to add for this module. These are copied to the build directory.</summary>
    public AccessLimitList<AdditionalDependency> AdditionalDependencies = new();

    /// <summary>The libraries to link.</summary>
    public AccessLimitList<string> Libraries = new();

    /// <summary>The library paths to search for. Absolute or relevant</summary>
    public AccessLimitList<string> LibrarySearchPaths = new();

    public List<string> SourceFiles = new();

    /// <summary>The name of the module. If null will automatically deduce the name from the file name.</summary> 
    public string? Name;

    public string OutputDirectory = "Binaries";

    /// <summary>The cpp standard this module uses.</summary>
    public CppStandards CppStandard = CppStandards.Cpp20;

    /// <summary> The type of this module</summary>
    public ModuleType Type;

    public ModuleContext Context;

    protected ModuleBase(ModuleContext context)
    {
        Context = context;
        SetOptions(context.Options);
    }

    public void PostConstruction()
    {
        Dependencies.Joined().ForEach(r => r.ResolveModulePath(this));
    }

    /*
     * Functions to check support.
     */
    public virtual bool IsPlatformSupported(PlatformBase inPlatformBase) => true;

    public virtual bool IsCompilerSupported(CompilerBase inCompilerBase) => true;

    public virtual bool IsArchitectureSupported(Architecture architecture) => true;


    public Dictionary<string, object?> GetOptions(bool onlyOutputChanging = false)
    {
        Dictionary<string, object?> d = new();
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

    private string GetOptionsString(bool onlyOutputChanging)
    {
        if (_optionsString != null)
            return _optionsString;
        var strBuilder = new StringBuilder();
        var serializer = new XmlSerializer(typeof(Tuple<string, object?>));
        using (var xmlWriter = XmlWriter.Create(strBuilder))
        {
            foreach (var option in GetOptions(onlyOutputChanging))
            {
                serializer.Serialize(xmlWriter, option);
            }
        }

        _optionsString = strBuilder.ToString();
        return _optionsString;
    }

    /**
     * Gets the id of the variant.
     */
    public int GetVariantId()
    {
        return GetOptionsString(false).GetHashCode();
    }

    private void SetOptions(Dictionary<string, string> options)
    {
        foreach (var field in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
        {
            var attr = field.GetCustomAttribute<ModuleOptionAttribute>();
            if (attr == null)
                continue;
            var name = attr.GetName(field);
            if (!options.TryGetValue(name, out var value) && attr.Required)
            {
                Context.AddMessage(ModuleContext.Message.SeverityTypes.Error,
                    $"Option {name}: Option is required but isn't supplied");
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
}