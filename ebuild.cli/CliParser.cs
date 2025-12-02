using System.Collections;
using Microsoft.VisualBasic;

namespace ebuild.cli;
/// <summary>
/// CLI Parser for commands and options.
/// </summary>
/// <typeparam name="T">the type of the root command</typeparam>
internal class CliParser
{
    CliParser(Type rootCommandType)
    {
        RootCommandType = rootCommandType;
        currentCommand = (Command)(Activator.CreateInstance(rootCommandType) ?? throw new InvalidOperationException($"Could not create instance of root command type {rootCommandType.FullName}."));
    }
    public Type RootCommandType;
    public Command currentCommand = new();
    public LinkedList<Command> currentCommandChain = [];

    public struct ParsedOption(string name, string? value, LinkedList<Command> currentCommandChain)
    {
        public string Name = name;
        public string? Value = value;
        public LinkedList<Command> CommandChain = new(currentCommandChain);
    }

    public struct ParsedArgument(string value, int order, LinkedList<Command> currentCommandChain)
    {
        public string Value = value;
        public int Order = order;
        public LinkedList<Command> CommandChain = new(currentCommandChain);
    }

    List<ParsedOption> parsedOptions = [];
    List<ParsedArgument> parsedArguments = [];

    public void Parse(string[] args)
    {
        var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current?.ToString();
            if (arg == null) continue;

            // Check if the argument matches a subcommand
            var subCommand = currentCommand.FindSubCommand(arg);
            if (subCommand != null)
            {
                currentCommand = subCommand;
                currentCommandChain.AddLast(currentCommand);
                continue;
            }
            var parsedOption = ParseOption(enumerator);
            // Read the options for the current command
            if (parsedOption != null)
            {
                // Process the parsed option (e.g., set the corresponding field in the command)
                parsedOptions.Add((ParsedOption)parsedOption);
                continue;
            }
            var parsedArgument = ParseArgument(enumerator);
            if (parsedArgument != null)
            {
                // Process the parsed argument (e.g., add to a list of arguments)
                parsedArguments.Add((ParsedArgument)parsedArgument);
                continue;
            }
        }
    }

    public ParsedOption? ParseOption(IEnumerator enumerator)
    {
        // Names can't start with a dash.
        // Examples:
        // -F or --Flag
        // -D Key=Value or --Define Key=Value
        // -D "Key=Value" or --Define "Key=Value"
        // -D"Key=Value" or --Define "Key=Value"
        // -DKey=Value or --Define Key=Value
        // -DKey or --Define Key

        var cur = enumerator.Current as string ?? throw new InvalidOperationException("Enumerator current is not a string.");
        if (!cur.StartsWith('-'))
            return null;
        bool isLong = cur.StartsWith("--");
        string name = isLong ? cur[2..] : cur[1..];
        if (isLong)
        {
            var value = ExpectValueString(enumerator);
            var parsed = new ParsedOption(name, value, currentCommandChain);
            return parsed;
        }

        // short options
        return null;

    }

    public string ExpectValueString(IEnumerator enumerator)
    {
        if (!enumerator.MoveNext())
            throw new InvalidOperationException("Expected a value but none was found.");
        var value = (enumerator.Current?.ToString()) ?? throw new InvalidOperationException("Expected a value but found null.");
        return value;
    }

    public ParsedArgument? ParseArgument(IEnumerator enumerator)
    {
        return null;
    }
}