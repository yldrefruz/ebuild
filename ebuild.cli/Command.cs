using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ebuild.cli
{
    public class Command
    {
        // Optional runtime overrides for name/aliases/description used for ghost commands
        public string? RuntimeName { get; set; }
        public string[]? RuntimeAliases { get; set; }
        public string? RuntimeDescription { get; set; }
        internal FieldInfo[] OptionFields => fieldInfosCache ??= GetOptionFields();

        internal HashSet<Command> subCommands { get; } = new HashSet<Command>();

        public Command()
        {
            RegisterNestedSubcommands();
        }

        private void RegisterNestedSubcommands()
        {
            var nested = GetType().GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var t in nested)
            {
                var attr = t.GetCustomAttribute<CommandAttribute>();
                if (attr != null && attr.AutoRegister == false) continue;
                if (!typeof(Command).IsAssignableFrom(t)) continue;
                if (t.IsAbstract) continue;
                try
                {
                    // If the nested command declares a multi-part name, only register it under the matching parent
                    if (attr != null && !string.IsNullOrEmpty(attr.Name) && attr.Name.Contains(' '))
                    {
                        var parts = attr.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var parentParts = parts.Take(parts.Length - 1).ToArray();
                            var myParts = GetCanonicalNameParts(this);
                            if (!myParts.SequenceEqual(parentParts, StringComparer.OrdinalIgnoreCase))
                            {
                                continue; // not intended for this parent
                            }
                        }
                    }

                    var inst = Activator.CreateInstance(t) as Command;
                    if (inst != null)
                        AddSubCommand(inst);
                }
                catch
                {
                    // ignore types that cannot be constructed
                }
            }
        }

        public void AddSubCommand(Command subCommand)
        {
            subCommands.Add(subCommand);
        }

        public Command? FindSubCommand(string name)
        {
            // Prefer attribute-based name resolution if available
            return subCommands.Where(c =>
            {
                // prefer runtime name if present
                if (!string.IsNullOrEmpty(c.RuntimeName))
                {
                    var parts = c.RuntimeName!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var local = parts.Length > 0 ? parts[^1] : c.RuntimeName;
                    if (string.Equals(local, name, StringComparison.OrdinalIgnoreCase)) return true;
                    if (c.RuntimeAliases != null && c.RuntimeAliases.Any(a =>
                    {
                        var ap = a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var al = ap.Length > 0 ? ap[^1] : a;
                        return string.Equals(al, name, StringComparison.OrdinalIgnoreCase);
                    })) return true;
                }

                var attr = c.GetType().GetCustomAttribute<CommandAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.Name))
                {
                    var parts = attr.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var local = parts.Length > 0 ? parts[^1] : attr.Name;
                    if (string.Equals(local, name, StringComparison.OrdinalIgnoreCase)) return true;
                    if (attr.Aliases != null && attr.Aliases.Any(a =>
                    {
                        var ap = a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var al = ap.Length > 0 ? ap[^1] : a;
                        return string.Equals(al, name, StringComparison.OrdinalIgnoreCase);
                    })) return true;
                }

                // fallback: type name
                return c.GetType().Name.Equals(name, StringComparison.OrdinalIgnoreCase);
            }).FirstOrDefault();
        }

        private static string[] GetCanonicalNameParts(Command cmd)
        {
            if (!string.IsNullOrEmpty(cmd.RuntimeName))
                return cmd.RuntimeName!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var attr = cmd.GetType().GetCustomAttribute<CommandAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.Name))
            {
                return attr.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return new[] { cmd.GetType().Name };
        }

        private FieldInfo[] GetOptionFields()
        {
            var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return fields.Where(f => f.GetCustomAttribute<OptionAttribute>() != null).ToArray();
        }

        private FieldInfo[]? fieldInfosCache;

        /// <summary>
        /// Asynchronously execute the command. Override in derived commands to perform work.
        /// Returns an integer exit code; 0 indicates success.
        /// </summary>
        public virtual Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}