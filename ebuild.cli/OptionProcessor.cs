using System.Reflection;

namespace ebuild.cli;



internal static class OptionProcessor
{
    internal static void ParseOption(CliParser parser, LinkedList<Command> commandChain, FieldInfo optionField, LinkedListNode<string>? currentArgNode, out LinkedListNode<string>? nextArgNode)
    {
        nextArgNode = currentArgNode;
        var optionAttribute = optionField.GetCustomAttribute<OptionAttribute>();
        if (optionAttribute == null)
        {
            throw new InvalidOperationException($"Field {optionField.Name} is not marked with OptionAttribute.");
        }

        // find the owner command of this option.
        var ownerCommand = commandChain.Where(c=>c.OptionFields.Contains(optionField)).FirstOrDefault() ?? throw new InvalidOperationException($"Option field {optionField.Name} does not belong to any command in the command chain.");
        if (OptionAttribute.IsFlag(optionField))
        {
            // Flag option
            nextArgNode = currentArgNode?.Next;
            return;
        }
        else if (OptionAttribute.IsMultiple(optionField))
        {
            // Multiple value option
            if (OptionAttribute.IsDictionary(optionField))
            {
                // Split by = for dictionary entries.
                // If there is a = in the value, it will be included as part of the value.
                // this can be escaped using \= in the value.
                var dict = (System.Collections.IDictionary?)Activator.CreateInstance(optionField.FieldType);
                var keyType = optionField.FieldType.GetGenericArguments()[0];
                var valueType = optionField.FieldType.GetGenericArguments()[1];

                while (nextArgNode != null)
                {
                    var entry = nextArgNode.Value;
                    var splitIndex = entry.IndexOf('=');
                    if (splitIndex < 0)
                    {
                        throw new InvalidOperationException($"Dictionary option {optionAttribute.Name ?? optionField.Name} requires entries in the format key=value.");
                    }

                    var keyString = entry.Substring(0, splitIndex);
                    var valueString = entry.Substring(splitIndex + 1).Replace(@"\=", "=");

                    var key = Convert.ChangeType(keyString, keyType);
                    var value = Convert.ChangeType(valueString, valueType);

                    dict!.Add(key, value);
                    nextArgNode = nextArgNode.Next;
                }
                return dict;
            }
            else
            {
                var values = (System.Collections.IList?)Activator.CreateInstance(optionField.FieldType);
                var elementType = optionField.FieldType.IsArray
                    ? optionField.FieldType.GetElementType()
                    : optionField.FieldType.GetGenericArguments()[0];

                int count = 0;
                while (nextArgNode != null && (optionAttribute.MaximumCount < 0 || count < optionAttribute.MaximumCount))
                {
                    var value = Convert.ChangeType(nextArgNode.Value, elementType!);
                    values!.Add(value);
                    count++;
                    nextArgNode = nextArgNode.Next;
                }

                if (count < optionAttribute.MinimumCount)
                {
                    throw new InvalidOperationException($"Option {optionAttribute.Name ?? optionField.Name} requires at least {optionAttribute.MinimumCount} values.");
                }

                return values;
            }

        }
        else
        {
            // Single value option
            if (nextArgNode == null)
            {
                throw new InvalidOperationException($"Option {optionAttribute.Name ?? optionField.Name} requires a value.");
            }

            var value = Convert.ChangeType(nextArgNode.Value, optionField.FieldType);
            nextArgNode = nextArgNode.Next;
            return value;
        }
    }
}