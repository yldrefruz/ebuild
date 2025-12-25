using System;

namespace ebuild.cli
{
    [Command("help", Aliases = new[] { "h" })]
    public class HelpCommand : Command
    {
        // marker command - behavior implemented by CliParser
    }
}
