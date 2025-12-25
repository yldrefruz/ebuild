using NUnit.Framework;
using ebuild.cli;
#pragma warning disable CS0649
namespace ebuild.Tests.Unit
{
    [TestFixture]
    public class CliParserSubcommandTests
    {
        [Command("sub", Aliases = new[] { "s" })]
        class SubCommand : Command
        {
            [Option("flag")]
            public bool Flag;
        }

        class RootWithSub : Command
        {
            // root has no options; subcommand added at runtime
        }

        class RootWithNested : Command
        {
            [Command("nested")]
            public class Nested : Command
            {
                [Option("flag")]
                public bool Flag;
            }
        }

        class RootWithNestedOptOut : Command
        {
            [Command("noauto", AutoRegister = false)]
            public class NoAuto : Command
            {
            }
        }

        [Test]
        public void Subcommand_switches_and_parses_option()
        {
            var parser = new CliParser(typeof(RootWithSub));
            // add a subcommand instance to the root created inside the parser
            var root = (RootWithSub)parser.currentCommandChain.First!.Value;
            var sub = new SubCommand();
            root.AddSubCommand(sub);

            parser.Parse(new[] { "cmd", "sub", "--flag" });
            parser.ApplyParsedToCommands();

            // ensure current command chain ended on the subcommand instance and its option was set
            var last = parser.currentCommandChain.Last!.Value as SubCommand;
            Assert.That(last, Is.Not.Null);
            Assert.That(last!.Flag, Is.True);
        }

        [Test]
        public void Subcommand_alias_is_recognized()
        {
            var parser = new CliParser(typeof(RootWithSub));
            var root = (RootWithSub)parser.currentCommandChain.First!.Value;
            var sub = new SubCommand();
            root.AddSubCommand(sub);

            parser.Parse(new[] { "cmd", "s", "--flag" });
            parser.ApplyParsedToCommands();

            var last = parser.currentCommandChain.Last!.Value as SubCommand;
            Assert.That(last, Is.Not.Null);
            Assert.That(last!.Flag, Is.True);
        }

        [Test]
        public void Nested_subcommand_is_auto_registered()
        {
            var parser = new CliParser(typeof(RootWithNested));
            parser.Parse(new[] { "cmd", "nested", "--flag" });
            parser.ApplyParsedToCommands();

            var last = parser.currentCommandChain.Last!.Value as RootWithNested.Nested;
            Assert.That(last, Is.Not.Null);
            Assert.That(last!.Flag, Is.True);
        }

        [Test]
        public void Nested_subcommand_opt_out_prevents_registration()
        {
            var parser = new CliParser(typeof(RootWithNestedOptOut));
            parser.Parse(new[] { "cmd", "noauto" });
            parser.ApplyParsedToCommands();

            // should remain on root because nested class opted out
            Assert.That(parser.currentCommandChain.Last!.Value.GetType(), Is.EqualTo(typeof(RootWithNestedOptOut)));
        }
    }
}
#pragma warning restore CS0649