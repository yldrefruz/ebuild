using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ebuild.cli
{
    /// <summary>
    /// CLI Parser for commands and options.
    /// Parse-first two-pass parser: tokenization then mapping.
    /// </summary>
    public enum DuplicateOptionPolicy
    {
        Error,
        Warn,
        Ignore
    }

    public enum UnknownOptionPolicy
    {
        Error,
        Warn,
        Ignore
    }

    public class CliParser
    {
        private readonly ILogger? _logger;
        public Type? RootCommandType { get; }
        public Command currentCommand { get; private set; }
        public LinkedList<Command> currentCommandChain { get; } = new LinkedList<Command>();

        public struct ParsedOption
        {
            public string Name { get; set; }
            public string? Value { get; set; }
            public LinkedList<Command> CommandChain { get; set; }
        }

        // Search the command hierarchy (root and its subcommands) for a field that declares
        // the option `name` and has OptionAttribute.Global == true. Returns true when found
        // and outputs the matching FieldInfo and Command instance.
        private bool FindGlobalOptionField(string name, out FieldInfo? fieldOut, out Command? cmdOut)
        {
            fieldOut = null;
            cmdOut = null;
            var root = currentCommandChain.First!.Value;

            FieldInfo? foundField = null;
            Command? foundCmd = null;

            bool Recurse(Command node)
            {
                foreach (var f in node.OptionFields)
                {
                    var attr = f.GetCustomAttribute<OptionAttribute>();
                    if (attr == null) continue;
                    if (!attr.Global) continue;
                    if (!string.IsNullOrEmpty(attr.Name) && string.Equals(attr.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        foundField = f;
                        foundCmd = node;
                        return true;
                    }
                    if (!string.IsNullOrEmpty(attr.ShortName) && string.Equals(attr.ShortName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        foundField = f;
                        foundCmd = node;
                        return true;
                    }
                    if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        foundField = f;
                        foundCmd = node;
                        return true;
                    }
                }

                foreach (var c in node.subCommands)
                {
                    if (Recurse(c)) return true;
                }
                return false;
            }

            var result = Recurse(root);
            fieldOut = foundField;
            cmdOut = foundCmd;
            return result;
        }

        public struct ParsedArgument
        {
            public string Value { get; set; }
            public int Order { get; set; }
            public LinkedList<Command> CommandChain { get; set; }
        }

        private readonly List<ParsedOption> parsedOptions = new List<ParsedOption>();
        private readonly List<ParsedArgument> parsedArguments = new List<ParsedArgument>();

        public IReadOnlyList<ParsedOption> ParsedOptions => parsedOptions;
        public IReadOnlyList<ParsedArgument> ParsedArguments => parsedArguments;

        private readonly DuplicateOptionPolicy duplicatePolicy;
        private readonly UnknownOptionPolicy unknownOptionPolicy;

        public CliParser(Type? rootCommandType = null, DuplicateOptionPolicy duplicateOptionPolicy = DuplicateOptionPolicy.Warn, UnknownOptionPolicy unknownOptionPolicy = UnknownOptionPolicy.Warn, ILogger? logger = null)
        {
            RootCommandType = rootCommandType;
            if (rootCommandType != null)
            {
                currentCommand = (Command)(Activator.CreateInstance(rootCommandType) ?? throw new InvalidOperationException($"Could not create instance of root command type {rootCommandType.FullName}."));
            }
            else
            {
                // create a ghost root command when no root type is supplied
                currentCommand = new Command();
                currentCommand.RuntimeName = ""; // unnamed ghost root
            }
            currentCommandChain.AddLast(currentCommand);
            duplicatePolicy = duplicateOptionPolicy;
            this.unknownOptionPolicy = unknownOptionPolicy;
            _logger = logger;
        }

        // When help is provided as the first argument (`prog help a b`), store the target tokens
        // so help resolution can occur later (after commands are registered).
        private List<string>? pendingHelpTargets;

        /// <summary>
        /// Scan an assembly for types deriving from `Command` and register them under the parser's root command,
        /// placing multi-part named commands under their parent according to space-separated name parts.
        /// Types with `CommandAttribute.AutoRegister == false` are skipped.
        /// </summary>
        public void RegisterCommandsFromAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var types = assembly.GetTypes().Where(t => typeof(Command).IsAssignableFrom(t) && !t.IsAbstract).ToList();

            // Build map fullName -> Type, skipping types that opted out via CommandAttribute.AutoRegister == false
            var nameToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in types)
            {
                var attr = t.GetCustomAttribute<CommandAttribute>();
                if (attr != null && attr.AutoRegister == false) continue;
                var fullName = (attr != null && !string.IsNullOrEmpty(attr.Name)) ? attr.Name : t.Name;
                if (!nameToType.ContainsKey(fullName))
                    nameToType[fullName] = t;
                else
                    LogWarning($"Duplicate command name '{fullName}' found in assembly {assembly.FullName}. Skipping type {t.FullName}.");
            }

            // Instances created so far by fullName
            var instances = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

            // root canonical name
            var rootAttr = currentCommand.GetType().GetCustomAttribute<CommandAttribute>();
            var rootFullName = !string.IsNullOrEmpty(currentCommand.RuntimeName) ? currentCommand.RuntimeName : (rootAttr != null && !string.IsNullOrEmpty(rootAttr.Name) ? rootAttr.Name : currentCommand.GetType().Name);
            instances[rootFullName] = currentCommand;

            // Process in order of increasing name-part length so parents are created before children
            foreach (var kv in nameToType.OrderBy(k => k.Key.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length))
            {
                var fullName = kv.Key;
                var t = kv.Value;
                // Skip the root type if present
                if (string.Equals(fullName, rootFullName, StringComparison.OrdinalIgnoreCase)) continue;

                var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string parentFullName;
                if (parts.Length == 1)
                    parentFullName = rootFullName;
                else
                    parentFullName = string.Join(' ', parts.Take(parts.Length - 1));

                if (!instances.TryGetValue(parentFullName, out var parent))
                {
                    // create missing parent chain (ghost commands) so help can be displayed
                    var parentParts = parentFullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string accum = string.Empty;
                    Command? lastParent = currentCommand;
                    for (int pi = 0; pi < parentParts.Length; pi++)
                    {
                        accum = pi == 0 ? parentParts[pi] : accum + " " + parentParts[pi];
                        if (instances.TryGetValue(accum, out var existing))
                        {
                            lastParent = existing;
                            continue;
                        }

                        // If there is a concrete type for this prefix, instantiate it; otherwise create a ghost Command
                        if (nameToType.TryGetValue(accum, out var concreteType))
                        {
                            try
                            {
                                var instConcrete = Activator.CreateInstance(concreteType) as Command;
                                if (instConcrete != null)
                                {
                                    lastParent.AddSubCommand(instConcrete);
                                    instances[accum] = instConcrete;
                                    lastParent = instConcrete;
                                    continue;
                                }
                            }
                            catch
                            {
                                // fall through to ghost
                            }
                        }

                        var ghost = new Command();
                        ghost.RuntimeName = accum;
                        lastParent.AddSubCommand(ghost);
                        instances[accum] = ghost;
                        lastParent = ghost;
                    }

                    parent = instances[parentFullName];
                }

                try
                {
                    var inst = Activator.CreateInstance(t) as Command;
                    if (inst == null)
                    {
                        LogWarning($"Could not instantiate command type {t.FullName}");
                        continue;
                    }

                    parent.AddSubCommand(inst);
                    instances[fullName] = inst;
                }
                catch (Exception ex)
                {
                    LogWarning($"Failed to create command {t.FullName}: {ex.Message}");
                }
            }
        }

        public void Parse(string[] args)
        {
            parsedOptions.Clear();
            parsedArguments.Clear();

            int argOrder = 0;
            bool endOfOptions = false;

            // Special-case: support `help` as the first argument: `help a b` -> show help for `a b`.
            // Defer resolution so command registration can occur after Parse.
            if (args.Length > 0)
            {
                var first = args[0];
                if (string.Equals(first, "help", StringComparison.OrdinalIgnoreCase) || string.Equals(first, "h", StringComparison.OrdinalIgnoreCase))
                {
                    pendingHelpTargets = new List<string>();
                    for (int j = 1; j < args.Length; j++)
                        pendingHelpTargets.Add(args[j]);
                    LogInfo($"Help requested for: {string.Join(' ', pendingHelpTargets)}");
                    return; // stop normal parsing; resolution will happen at execution time
                }
            }

            // Iterate over all provided args (Main(string[] args) does not include program name)
            for (int i = 0; i < args.Length; i++)
            {
                var raw = args[i];
                if (raw == null) continue;

                // Check for subcommand first (literal name match)
                var subCommand = currentCommand.FindSubCommand(raw);
                if (subCommand != null)
                {
                    currentCommand = subCommand;
                    currentCommandChain.AddLast(currentCommand);
                    continue;
                }

                if (!endOfOptions && raw == "--")
                {
                    endOfOptions = true;
                    continue;
                }

                if (!endOfOptions)
                {
                    if (IsNegativeNumber(raw) && !HasMatchingOptionForToken(raw))
                    {
                        parsedArguments.Add(new ParsedArgument { Value = raw, Order = argOrder++, CommandChain = new LinkedList<Command>(currentCommandChain) });
                        continue;
                    }

                    if (IsOptionToken(raw))
                    {
                        var opt = ParseOptionToken(args, ref i);
                        if (opt.HasValue)
                        {
                            var o = opt.Value;
                            o.Value = Unquote(o.Value);
                            o.CommandChain = new LinkedList<Command>(currentCommandChain);
                            parsedOptions.Add(o);
                            continue;
                        }
                    }
                }

                // Otherwise positional argument
                parsedArguments.Add(new ParsedArgument { Value = raw, Order = argOrder++, CommandChain = new LinkedList<Command>(currentCommandChain) });
            }
        }

        private bool HasMatchingOptionForToken(string token)
        {
            // token starts with '-'
            var stripped = token.TrimStart('-');
            // Check current command's option fields for Name or ShortName matching
            var fields = currentCommand.OptionFields;
            foreach (var f in fields)
            {
                var attr = f.GetCustomAttribute<OptionAttribute>();
                if (attr == null) continue;
                if (!string.IsNullOrEmpty(attr.Name) && string.Equals(attr.Name, stripped, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(attr.ShortName) && string.Equals(attr.ShortName, stripped, StringComparison.OrdinalIgnoreCase)) return true;
                // also allow single-char short without attribute as field name match
                if (f.Name.Equals(stripped, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private bool IsOptionToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (!token.StartsWith("-")) return false;
            // token is option-like (starts with -), but we will not expand combined short flags.
            return true;
        }

        private bool HasShortOption(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return false;
            var fields = currentCommand.OptionFields;
            foreach (var f in fields)
            {
                var attr = f.GetCustomAttribute<OptionAttribute>();
                if (attr == null) continue;
                if (!string.IsNullOrEmpty(attr.ShortName) && string.Equals(attr.ShortName, shortName, StringComparison.OrdinalIgnoreCase)) return true;
                if (f.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private ParsedOption? ParseOptionToken(string token, IEnumerator enumerator)
        {
            // Long option: --name or --name=value
            if (token.StartsWith("--"))
            {
                var body = token.Substring(2);
                var idx = body.IndexOf('=');
                if (idx >= 0)
                {
                    var name = body.Substring(0, idx);
                    var val = body.Substring(idx + 1);
                    return new ParsedOption { Name = name, Value = val };
                }
                else
                {
                    // Take next token as value if it doesn't start with -
                    var name = body;
                    return new ParsedOption { Name = name, Value = null };
                }
            }

            // Short-ish option: -x or -name (we disallow combined expansion -abc)
            if (token.StartsWith("-"))
            {
                var body = token.Substring(1);
                var idx = body.IndexOf('=');
                if (idx >= 0)
                {
                    var name = body.Substring(0, idx);
                    var val = body.Substring(idx + 1);
                    return new ParsedOption { Name = name, Value = val };
                }
                // If body length==1 treat as short flag possibly with separated value
                if (body.Length == 1)
                {
                    var name = body;
                    return new ParsedOption { Name = name, Value = null };
                }
                else
                {
                    // body longer than 1 and no '=' -> treat as single option name (disallow combined short flags)
                    return new ParsedOption { Name = body, Value = null };
                }
            }

            return null;
        }

        // index-based overload used by the Parse loop so we can consume following tokens when needed
        private ParsedOption? ParseOptionToken(string[] args, ref int i)
        {
            var token = args[i];
            if (token.StartsWith("--"))
            {
                var body = token.Substring(2);
                var idx = body.IndexOf('=');
                if (idx >= 0)
                {
                    var name = body.Substring(0, idx);
                    var val = body.Substring(idx + 1);
                    return new ParsedOption { Name = name, Value = val };
                }
                else
                {
                    var name = body;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        i++;
                        return new ParsedOption { Name = name, Value = args[i] };
                    }
                    return new ParsedOption { Name = name, Value = null };
                }
            }

            if (token.StartsWith("-"))
            {
                var body = token.Substring(1);
                var idx = body.IndexOf('=');
                if (idx >= 0)
                {
                    // Prefer short-name semantics for -DKey=Value => short name 'D', value 'Key=Value' when possible
                    var candidateShort = body.Substring(0, 1);
                    if (HasShortOption(candidateShort))
                    {
                        var name = candidateShort;
                        var val = body.Substring(1); // keep the rest (including any '=')
                        return new ParsedOption { Name = name, Value = val };
                    }

                    var nameLong = body.Substring(0, idx);
                    var valLong = body.Substring(idx + 1);
                    return new ParsedOption { Name = nameLong, Value = valLong };
                }

                if (body.Length == 1)
                {
                    var name = body;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        i++;
                        return new ParsedOption { Name = name, Value = args[i] };
                    }
                    return new ParsedOption { Name = name, Value = null };
                }
                else
                {
                    // treat as -Dkey (short name + attached value) when first char matches a short option
                    var candidateShort = body.Substring(0, 1);
                    if (HasShortOption(candidateShort))
                    {
                        var name = candidateShort;
                        var val = body.Substring(1);
                        return new ParsedOption { Name = name, Value = val };
                    }
                    return new ParsedOption { Name = body, Value = null };
                }
            }

            return null;
        }

        private static string? Unquote(string? s)
        {
            if (s == null) return null;
            if (s.Length >= 2 && ((s.StartsWith("\"") && s.EndsWith("\"")) || (s.StartsWith("'") && s.EndsWith("'"))))
                return s.Substring(1, s.Length - 2);
            return s;
        }

        private static bool IsNegativeNumber(string token)
        {
            // simple heuristic: -123 or -123.45
            if (string.IsNullOrEmpty(token)) return false;
            if (!token.StartsWith("-")) return false;
            var rest = token.Substring(1);
            return double.TryParse(rest, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
        }

        // Mapping pass: map parsedOptions/parsedArguments to command fields and run converters
        public void ApplyParsedToCommands()
        {
            var optionGroups = parsedOptions.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var group in optionGroups)
            {
                var name = group.Key;
                FieldInfo? targetField = null;
                Command? targetCommandInstance = null;

                foreach (var cmd in currentCommandChain.Reverse())
                {
                    var fields = cmd.OptionFields;
                    foreach (var f in fields)
                    {
                        var attr = f.GetCustomAttribute<OptionAttribute>();
                        if (attr == null) continue;
                        if (!string.IsNullOrEmpty(attr.Name) && string.Equals(attr.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            targetField = f;
                            targetCommandInstance = cmd;
                            break;
                        }
                        if (!string.IsNullOrEmpty(attr.ShortName) && string.Equals(attr.ShortName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            targetField = f;
                            targetCommandInstance = cmd;
                            break;
                        }
                        if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            targetField = f;
                            targetCommandInstance = cmd;
                            break;
                        }
                    }
                    if (targetField != null) break;
                }

                if (targetField == null)
                {
                    // If not found in the current command chain, check for a globally-declared option
                    var globalFound = FindGlobalOptionField(name, out FieldInfo? globalField, out Command? globalCmd);
                    if (globalFound)
                    {
                        targetField = globalField;
                        targetCommandInstance = globalCmd;
                    }

                    if (targetField == null)
                    {
                        switch (unknownOptionPolicy)
                    {
                        case UnknownOptionPolicy.Error:
                            throw new InvalidOperationException($"Unknown option: {name}");
                        case UnknownOptionPolicy.Warn:
                            LogWarning($"Unknown option '{name}' for command {currentCommand.GetType().Name}. Ignoring.");
                            break;
                        case UnknownOptionPolicy.Ignore:
                            break;
                    }
                    continue;
                    }
                }

                var attrOpt = targetField.GetCustomAttribute<OptionAttribute>()!;
                var values = group.Select(g => g.Value).ToList();

                if (OptionAttribute.IsFlag(targetField))
                {
                    var lastVal = values.LastOrDefault();
                    object finalBool = true;
                    if (!string.IsNullOrEmpty(lastVal))
                    {
                        if (!bool.TryParse(lastVal, out var b))
                            throw new InvalidOperationException($"Could not parse boolean for option {name}: '{lastVal}'");
                        finalBool = b;
                    }
                    targetField.SetValue(targetCommandInstance, finalBool);
                    continue;
                }

                if (OptionAttribute.IsMultiple(targetField) && !OptionAttribute.IsDictionary(targetField))
                {
                    var fieldVal = targetField.GetValue(targetCommandInstance);
                    var fType = targetField.FieldType;
                    if (fieldVal == null)
                    {
                        if (fType.IsInterface && fType.IsGenericType)
                        {
                            var elemType = fType.GetGenericArguments()[0];
                            var listType = typeof(List<>).MakeGenericType(elemType);
                            fieldVal = Activator.CreateInstance(listType);
                            targetField.SetValue(targetCommandInstance, fieldVal);
                        }
                    }

                    if (fieldVal is System.Collections.IList list)
                    {
                        var elemType = targetField.FieldType.IsArray ? targetField.FieldType.GetElementType() : targetField.FieldType.IsGenericType ? targetField.FieldType.GetGenericArguments()[0] : typeof(object);
                        foreach (var v in values)
                        {
                            var conv = ConvertStringToType(v, elemType!, attrOpt.ConverterType);
                            list.Add(conv);
                        }
                        continue;
                    }

                    throw new InvalidOperationException($"Cannot append values to field {targetField.Name}");
                }

                if (OptionAttribute.IsDictionary(targetField))
                {
                    var fieldVal = targetField.GetValue(targetCommandInstance);
                    var fType = targetField.FieldType;
                    Type keyType;
                    Type valType;

                    if (fieldVal == null)
                    {
                        if (fType.IsInterface && fType.IsGenericType)
                        {
                            var args = fType.GetGenericArguments();
                            keyType = args[0];
                            valType = args[1];
                            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valType);
                            fieldVal = Activator.CreateInstance(dictType);
                            targetField.SetValue(targetCommandInstance, fieldVal);
                        }
                        else if (fType.IsGenericType)
                        {
                            var args = fType.GetGenericArguments();
                            keyType = args[0];
                            valType = args[1];
                            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valType);
                            fieldVal = Activator.CreateInstance(dictType);
                            targetField.SetValue(targetCommandInstance, fieldVal);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Cannot create dictionary for field {targetField.Name}");
                        }
                    }

                    if (fieldVal is System.Collections.IDictionary dict)
                    {
                        var genArgs = fieldVal.GetType().IsGenericType ? fieldVal.GetType().GetGenericArguments() : fType.IsGenericType ? fType.GetGenericArguments() : new Type[] { typeof(string), typeof(string) };
                        keyType = genArgs.Length > 0 ? genArgs[0] : typeof(string);
                        valType = genArgs.Length > 1 ? genArgs[1] : typeof(string);

                        foreach (var v in values)
                        {
                            if (string.IsNullOrEmpty(v)) continue;
                            var idx = v.IndexOf('=');
                            if (idx <= 0) throw new InvalidOperationException($"Invalid dictionary entry for option {name}: '{v}'");
                            var keyStr = v.Substring(0, idx);
                            var valStr = v.Substring(idx + 1);
                            var keyObj = ConvertStringToType(keyStr, keyType, null);
                            var valObj = ConvertStringToType(valStr, valType, attrOpt.ConverterType);
                            dict[keyObj!] = valObj!;
                        }
                        continue;
                    }

                    throw new InvalidOperationException($"Cannot assign dictionary values to field {targetField.Name}");
                }

                if (values.Count > 1)
                {
                    if (duplicatePolicy == DuplicateOptionPolicy.Error)
                        throw new InvalidOperationException($"Multiple occurrences of option '{name}' not allowed.");
                    if (duplicatePolicy == DuplicateOptionPolicy.Warn)
                        LogWarning($"Multiple occurrences of option '{name}' for command {currentCommand.GetType().Name} — using last value.");
                }

                var last = values.LastOrDefault();
                var converted = ConvertStringToType(last, targetField.FieldType, attrOpt.ConverterType);
                targetField.SetValue(targetCommandInstance, converted);
            }

            // Map positional arguments using ArgumentAttribute
            var argsByCommand = parsedArguments.GroupBy(a => a.CommandChain.Last!.Value);
            foreach (var grp in argsByCommand)
            {
                var cmd = grp.Key;
                var argList = grp.OrderBy(a => a.Order).Select(a => a.Value).ToList();
                var fields = cmd.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => new { Field = f, Attr = f.GetCustomAttribute<ArgumentAttribute>() })
                    .Where(x => x.Attr != null)
                    .OrderBy(x => x.Attr!.Order)
                    .ToList();

                int ai = 0;
                foreach (var entry in fields)
                {
                    var f = entry.Field;
                    var attr = entry.Attr!;
                    if (attr.AllowMultiple)
                    {
                        // assign the rest
                        var remaining = argList.Skip(ai).ToList();
                        if (remaining.Count == 0 && attr.IsRequired)
                            throw new InvalidOperationException($"Missing required positional argument '{attr.Name ?? f.Name}' for command {cmd.GetType().Name}");

                        if (remaining.Count > 0)
                        {
                            if (OptionAttribute.IsMultiple(f) || typeof(System.Collections.IList).IsAssignableFrom(f.FieldType))
                            {
                                var elemType = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType.IsGenericType ? f.FieldType.GetGenericArguments()[0] : typeof(string);
                                var listType = typeof(List<>).MakeGenericType(elemType!);
                                var list = (System.Collections.IList?)Activator.CreateInstance(listType);
                                foreach (var v in remaining)
                                {
                                    list!.Add(ConvertStringToType(v, elemType!, null));
                                }
                                f.SetValue(cmd, list);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Field {f.Name} is not a collection but marked AllowMultiple");
                            }
                        }
                        ai = argList.Count; // consumed all
                        break;
                    }
                    else
                    {
                        if (ai >= argList.Count)
                        {
                            if (attr.IsRequired)
                                throw new InvalidOperationException($"Missing required positional argument '{attr.Name ?? f.Name}' for command {cmd.GetType().Name}");
                            break;
                        }
                        var val = argList[ai++];
                        var conv = ConvertStringToType(val, f.FieldType, null);
                        f.SetValue(cmd, conv);
                    }
                }
            }
        }

        /// <summary>
        /// Execute the current command (the last in the current command chain).
        /// If the command is the `help` command, render help for the parent command instead.
        /// Returns an integer exit code (0 success).
        /// </summary>
        public async Task<int> ExecuteCurrentCommandAsync(CancellationToken cancellationToken = default)
        {
            // If help was requested as the first argument, resolve the desired command chain now
            if (pendingHelpTargets != null)
            {
                // build chain from root using registered commands
                var chain = new LinkedList<Command>();
                chain.AddLast(currentCommandChain.First!.Value);
                foreach (var tok in pendingHelpTargets)
                {
                    var sub = chain.Last!.Value.FindSubCommand(tok);
                    if (sub == null) break;
                    chain.AddLast(sub);
                }
                // append HelpCommand
                chain.AddLast(new HelpCommand());

                LogInfo($"Resolved help chain: {string.Join(" -> ", chain.Select(c => !string.IsNullOrEmpty(c.RuntimeName) ? c.RuntimeName : (c.GetType().GetCustomAttribute<CommandAttribute>()?.Name ?? c.GetType().Name)))}");
                currentCommandChain.Clear();
                foreach (var c in chain) currentCommandChain.AddLast(c);
                currentCommand = currentCommandChain.Last!.Value;
                pendingHelpTargets = null;
            }

            var last = currentCommandChain.Last!.Value;
            // If root is a ghost root (unnamed) and no subcommand was provided, show help
            if (currentCommandChain.Count == 1 && string.IsNullOrEmpty(currentCommandChain.First!.Value.RuntimeName))
            {
                RenderHelpForCommand(currentCommandChain.First!.Value);
                return 0;
            }

            if (last is HelpCommand)
            {
                var parentNode = currentCommandChain.Last.Previous;
                var parent = parentNode != null ? parentNode.Value : currentCommandChain.First!.Value;
                RenderHelpForCommand(parent);
                return 0;
            }

            // call command's execution method
            try
            {
                return await last.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWarning($"Command execution failed: {ex.Message}");
                return 1;
            }
        }

        private void RenderHelpForCommand(Command cmd)
        {
            void Write(string s)
            {
                if (_logger != null) _logger.LogInformation(s);
                else Console.WriteLine(s);
            }

            Write($"Usage: {cmd.GetType().Name}");

            // Subcommands
            var subs = cmd.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Where(f => false); // placeholder to keep formatting consistent
            Write("\nCommands:");
            foreach (var s in cmd.subCommands)
            {
                var attr = s.GetType().GetCustomAttribute<CommandAttribute>();
                var name = !string.IsNullOrEmpty(s.RuntimeName) ? s.RuntimeName! : (attr != null && !string.IsNullOrEmpty(attr.Name) ? attr.Name : s.GetType().Name);
                var aliasesArr = s.RuntimeAliases ?? attr?.Aliases;
                var aliases = aliasesArr != null && aliasesArr.Length > 0 ? $" (aliases: {string.Join(",", aliasesArr)})" : string.Empty;
                var desc = !string.IsNullOrEmpty(s.RuntimeDescription) ? $" - {s.RuntimeDescription}" : (attr != null && !string.IsNullOrEmpty(attr.Description) ? $" - {attr.Description}" : string.Empty);
                Write($"  {name}{aliases}{desc}");
            }

            // Options
            Write("\nOptions:");
            var opts = cmd.OptionFields;
            foreach (var f in opts)
            {
                var a = f.GetCustomAttribute<OptionAttribute>();
                if (a == null) continue;
                var longName = !string.IsNullOrEmpty(a.Name) ? a.Name : f.Name;
                var shortName = !string.IsNullOrEmpty(a.ShortName) ? $", -{a.ShortName}" : string.Empty;
                var desc = !string.IsNullOrEmpty(a.Description) ? $" - {a.Description}" : string.Empty;
                Write($"  --{longName}{shortName} : {GetFriendlyTypeName(f.FieldType)}{desc}");
            }

            // Arguments
            Write("\nArguments:");
            var argFields = cmd.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(f => new { Field = f, Attr = f.GetCustomAttribute<ArgumentAttribute>() })
                .Where(x => x.Attr != null)
                .OrderBy(x => x.Attr!.Order);
            foreach (var entry in argFields)
            {
                var attr = entry.Attr!;
                var desc = !string.IsNullOrEmpty(attr.Description) ? $" - {attr.Description}" : string.Empty;
                Write($"  {attr.Order}: {attr.Name ?? entry.Field.Name} ({GetFriendlyTypeName(entry.Field.FieldType)}){(attr.IsRequired ? " [required]" : string.Empty)}{desc}");
            }
        }

        private string GetFriendlyTypeName(Type t)
        {
            if (t.IsArray)
            {
                var elem = GetFriendlyTypeName(t.GetElementType()!);
                return elem + "[]";
            }

            var nonNullable = Nullable.GetUnderlyingType(t) ?? t;
            if (nonNullable.IsGenericType)
            {
                var genDef = nonNullable.GetGenericTypeDefinition();
                var name = genDef.Name;
                var backTick = name.IndexOf('`');
                if (backTick >= 0) name = name.Substring(0, backTick);
                var args = nonNullable.GetGenericArguments().Select(a => GetFriendlyTypeName(a));
                return name + "<" + string.Join(", ", args) + ">";
            }

            return nonNullable.Name;
        }

        private object? ConvertStringToType(string? value, Type targetType, Type? converterType)
        {
            if (value == null)
            {
                if (Nullable.GetUnderlyingType(targetType) != null || targetType.IsClass) return null;
                throw new InvalidOperationException($"Cannot assign null to non-nullable type {targetType}");
            }

            if (converterType != null)
            {
                var inst = Activator.CreateInstance(converterType) ?? throw new InvalidOperationException($"Could not create converter {converterType}");
                if (inst is IStringConverter conv)
                {
                    return conv.Convert(value);
                }
                throw new InvalidOperationException($"Converter {converterType} does not implement IStringConverter");
            }

            var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (nonNullable == typeof(string)) return value;
            if (nonNullable.IsEnum) return Enum.Parse(nonNullable, value, true);
            if (nonNullable == typeof(bool)) return bool.Parse(value);
            if (nonNullable == typeof(int)) return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (nonNullable == typeof(long)) return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (nonNullable == typeof(double)) return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (nonNullable == typeof(decimal)) return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            if (nonNullable == typeof(Guid)) return Guid.Parse(value);
            // Fallback to ChangeType
            if (typeof(IConvertible).IsAssignableFrom(nonNullable))
                return Convert.ChangeType(value, nonNullable, System.Globalization.CultureInfo.InvariantCulture);

            throw new InvalidOperationException($"No conversion available for type {targetType}");
        }

        private void LogWarning(string message)
        {
            if (_logger != null)
            {
                _logger.LogWarning(message);
            }
            else
            {
                Console.WriteLine("Warning: " + message);
            }
        }

        private void LogInfo(string message)
        {
            if (_logger != null)
            {
                _logger.LogInformation(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }
    }
}