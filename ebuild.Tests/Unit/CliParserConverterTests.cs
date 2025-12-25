using NUnit.Framework;
using System.Collections.Generic;
using ebuild.cli;
#pragma warning disable CS0649
namespace ebuild.Tests.Unit
{
    [TestFixture]
    public class CliParserConverterTests
    {
        // Simple converter used by tests
        public class UpperCaseConverter : IStringConverter
        {
            public object Convert(string value) => value.ToUpperInvariant();
        }

        class TestConvRoot : Command
        {
            [Option("opt", ConverterType = typeof(UpperCaseConverter))]
            public string? Opt;
        }

        [Test]
        public void Uses_IStringConverter_for_field()
        {
            var parser = new CliParser(typeof(TestConvRoot));
            parser.Parse(new[] { "cmd", "--opt=hello" });
            parser.ApplyParsedToCommands();
            var root = (TestConvRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Opt, Is.EqualTo("HELLO"));
        }

        class TestDictRoot : Command
        {
            [Option("map")]
            public Dictionary<string, int>? Map;
        }

        [Test]
        public void Dictionary_values_are_converted_to_int()
        {
            var parser = new CliParser(typeof(TestDictRoot));
            parser.Parse(new[] { "cmd", "--map=foo=42", "--map=bar=7" });
            parser.ApplyParsedToCommands();
            var root = (TestDictRoot)parser.currentCommandChain.First!.Value;
            Assert.That(root.Map!.ContainsKey("foo") && root.Map["foo"] == 42);
            Assert.That(root.Map!.ContainsKey("bar") && root.Map["bar"] == 7);
        }
    }
}

#pragma warning restore CS0649