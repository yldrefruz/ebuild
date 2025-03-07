using System.CommandLine;

namespace ebuild.Commands;

public class PropertyCommand
{
    private readonly Command _command = new("property",
        "operations for properties. These are useful for creation of custom scripts or using ebuild without referencing, directly from command line");

    public PropertyCommand()
    {
        _command.AddCommand(new GetCommand());
    }

    private class GetCommand
    {
        private readonly Command _command = new("get", "get the value of a property");

        private readonly Argument<string> _propertyName =
            new Argument<string>("name", "the name of the property").FromAmong(
                "ebuild.api.dll"
            );

        public GetCommand()
        {
            _command.AddArgument(_propertyName);
            _command.SetHandler((context) =>
            {
                if (context.ParseResult.GetValueForArgument(_propertyName) == "ebuild.api.dll")
                {
                    Console.WriteLine(EBuild.FindEBuildApiDllPath());
                }
            });
        }

        public static implicit operator Command(GetCommand gc) => gc._command;
    }

    public static implicit operator Command(PropertyCommand pc) => pc._command;
}