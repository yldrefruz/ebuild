using System.Reflection;

namespace ebuild.cli;




public class Command
{
    internal FieldInfo[] OptionFields => fieldInfosCache ??= GetOptionFields();
    

    internal HashSet<Command> subCommands = [];



    public void AddSubCommand(Command subCommand)
    {
        subCommands.Add(subCommand);
    }

    public Command? FindSubCommand(string name)
    {
        return subCommands.Where(c => c.GetType().Name.Equals(name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
    }

    private FieldInfo[] GetOptionFields()
    {
        var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return [.. fields.Where(f => f.GetCustomAttribute<OptionAttribute>() != null)];
    }
    private FieldInfo[]? fieldInfosCache;
}