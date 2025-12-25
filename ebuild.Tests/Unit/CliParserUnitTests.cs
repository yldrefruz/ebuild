using NUnit.Framework;
using System;
using ebuild.cli;

namespace ebuild.Tests.Unit
{
    [TestFixture]
    public class CliParserTests
    {
        class TestRoot : Command
        {
            [Option("verbose", ShortName = "v")]
            public bool Verbose;

            [Option("define", ShortName = "D")]
            public string? Define;
            [Argument(0, Name = "input")]
            public string? Input;
        }

        [Test]
        public void Parses_long_option_with_equals()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "--define=Z=1" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Define, Is.EqualTo("Z=1"));
        }

        [Test]
        public void Negative_number_is_argument_when_no_option()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "-42", "foo" });
            parser.ApplyParsedToCommands();
            Assert.That(parser.ParsedOptions.Count, Is.EqualTo(0));
            Assert.That(parser.ParsedArguments.Count, Is.EqualTo(2));
            Assert.That(parser.ParsedArguments[0].Value, Is.EqualTo("-42"));
        }

        [Test]
        public void Positional_argument_maps_to_field()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "input.txt" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Input, Is.EqualTo("input.txt"));
        }

        [Test]
        public void Duplicate_option_policy_error_throws()
        {
            var parser = new CliParser(typeof(TestRoot), DuplicateOptionPolicy.Error);
            parser.Parse(new[] { "cmd", "--define=one", "--define=two" });
            Assert.Throws<InvalidOperationException>(() => parser.ApplyParsedToCommands());
        }

        [Test]
        public void Duplicate_option_policy_warn_last_wins()
        {
            var parser = new CliParser(typeof(TestRoot), DuplicateOptionPolicy.Warn);
            parser.Parse(new[] { "cmd", "--define=one", "--define=two" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Define, Is.EqualTo("two"));
        }

        [Test]
        public void Duplicate_option_policy_ignore_last_wins()
        {
            var parser = new CliParser(typeof(TestRoot), DuplicateOptionPolicy.Ignore);
            parser.Parse(new[] { "cmd", "--define=one", "--define=two" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Define, Is.EqualTo("two"));
        }

        [Test]
        public void Disallow_combined_short_flags()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "-abc" });
            parser.ApplyParsedToCommands();
            // Should treat as a single option name 'abc'
            Assert.That(parser.ParsedOptions.Count, Is.EqualTo(1));
            Assert.That(parser.ParsedOptions[0].Name, Is.EqualTo("abc"));
        }

        [Test]
        public void Short_option_attached_value_with_equals_parses()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "-DZLIB=1.2.11" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Define, Is.EqualTo("ZLIB=1.2.11"));
        }

        [Test]
        public void Short_option_attached_quoted_value_parses()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "-D\"USE_ZLIB\"" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Define, Is.EqualTo("USE_ZLIB"));
        }

        [Test]
        public void Short_option_attached_quoted_value_with_spaces_parses()
        {
            var parser = new CliParser(typeof(TestRoot));
            parser.Parse(new[] { "cmd", "-D\"Use ZLIB Data\"" });
            parser.ApplyParsedToCommands();
            var root = (TestRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Define, Is.EqualTo("Use ZLIB Data"));
        }
    }
}
