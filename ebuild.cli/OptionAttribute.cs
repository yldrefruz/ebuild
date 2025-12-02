using System.Reflection;

namespace ebuild.cli;

[AttributeUsage(AttributeTargets.Field)]
public class OptionAttribute : Attribute
{
    public string? Name { get; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    public int MinimumCount { get; set; } = 0;
    public int MaximumCount { get; set; } = int.MaxValue;
    public Type? ConverterType { get; set; } = null;
    public bool IsLocalOnly { get; set; } = false;
    public OptionAttribute()
    {
        Name = null;
    }

    public OptionAttribute(string name)
    {
        Name = name;
    }


    public static bool IsFlag(FieldInfo fieldInfo)
    {
        return fieldInfo.FieldType == typeof(bool) || typeof(bool?).IsAssignableFrom(fieldInfo.FieldType);
    }

    public static bool IsMultiple(FieldInfo fieldInfo)
    {
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(fieldInfo.FieldType) && fieldInfo.FieldType != typeof(string);
    }

    public bool IsRequired(FieldInfo fieldInfo)
    {
        return !(IsMultiple(fieldInfo) && MinimumCount > 0) && !IsFlag(fieldInfo) && !(Nullable.GetUnderlyingType(fieldInfo.FieldType) != null || fieldInfo.FieldType.IsClass);
    }

    public static bool IsDictionary(FieldInfo fieldInfo)
    {
        return typeof(System.Collections.IDictionary).IsAssignableFrom(fieldInfo.FieldType);
    }
}