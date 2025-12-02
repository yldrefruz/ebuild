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

}