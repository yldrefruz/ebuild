using System.Reflection;

namespace ebuild.cli;




public class Command
{
    internal FieldInfo[] OptionFields => fieldInfosCache ??= GetOptionFields();



    private FieldInfo[] GetOptionFields()
    {
        
    }
    private FieldInfo[]? fieldInfosCache;
}