using System.Reflection;

namespace ebuild.cli;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class OptionAttribute : Attribute
{
    public string? Name { get; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
    // When true, this option may be provided at any position and will be applied
    // to the command that declares it (useful for root/global flags like verbose).
    public bool Global { get; set; } = false;
    public int MinimumCount { get; set; } = 0;
    public int MaximumCount { get; set; } = int.MaxValue;
    public Type? ConverterType { get; set; } = null;
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
        if (IsDictionary(fieldInfo)) return false;
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(fieldInfo.FieldType) && fieldInfo.FieldType != typeof(string);
    }

    public bool IsRequired(FieldInfo fieldInfo)
    {
        return !(IsMultiple(fieldInfo) && MinimumCount > 0) && !IsFlag(fieldInfo) && !(Nullable.GetUnderlyingType(fieldInfo.FieldType) != null || fieldInfo.FieldType.IsClass);
    }

    public static bool IsDictionary(FieldInfo fieldInfo)
    {
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(fieldInfo.FieldType)) return true;
        // detect generic IDictionary<TKey, TValue>
        return fieldInfo.FieldType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IDictionary<,>));
    }
}