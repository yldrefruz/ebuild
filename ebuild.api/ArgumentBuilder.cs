namespace ebuild.api
{
    /// <summary>
    /// Helper that builds a list of command-line arguments.
    /// The API is chainable so calls like <c>new ArgumentBuilder().Add("-I/include").Add("-DDEBUG")</c>
    /// are supported and will produce a combined argument list.
    /// </summary>
    public class ArgumentBuilder
    {
        /// <summary>
        /// Adds a single argument to the builder.
        /// </summary>
        /// <param name="command">The argument string to add (e.g. "-Iinclude" or "--flag").</param>
        /// <returns>The same <see cref="ArgumentBuilder"/> instance to allow chaining.</returns>
        public ArgumentBuilder Add(string command)
        {
            _args.Add(command);
            return this;
        }

        /// <summary>
        /// Adds a sequence of arguments to the builder.
        /// </summary>
        /// <param name="commands">An enumerable of argument strings to append.</param>
        /// <returns>The same <see cref="ArgumentBuilder"/> instance to allow chaining.</returns>
        public ArgumentBuilder AddRange(IEnumerable<string> commands)
        {
            _args.AddRange(commands);
            return this;
        }

        /// <summary>
        /// Removes the first occurrence of <paramref name="command"/> from the argument list.
        /// </summary>
        /// <param name="command">The argument string to remove.</param>
        /// <returns>The same <see cref="ArgumentBuilder"/> instance to allow chaining.</returns>
        public ArgumentBuilder Remove(string command)
        {
            _args.Remove(command);
            return this;
        }

        /// <summary>
        /// Backing storage for arguments.
        /// </summary>
        private readonly List<string> _args = [];



        /// <summary>
        /// Returns a copy of the arguments as an array of strings.
        /// </summary>
        /// <returns>A new <see cref="string[]"/> containing the current arguments in order.</returns>
        public string[] AsStringArray() => [.. _args];

        /// <summary>
        /// Formats the argument list into a single command-line string.
        /// Individual arguments containing whitespace are quoted. Existing surrounding
        /// quotes are preserved when possible.
        /// </summary>
        /// <returns>A single space-separated string representing the argument list.</returns>
        public override string ToString()
        {
            var str = string.Empty;
            foreach (var argument in _args)
            {
                var mutable = argument.Trim();
                char? inQuoteType = null;
                var requiresQuotes = false;
                foreach (var c in mutable)
                {
                    if (char.IsWhiteSpace(c) && inQuoteType == null)
                    {
                        requiresQuotes = true;
                        break;
                    }

                    if (inQuoteType == null && c is '"' or '\'')
                    {
                        inQuoteType = c;
                        continue;
                    }

                    if (c != inQuoteType) continue;
                    inQuoteType = null;
                }

                if (requiresQuotes)
                {
                    mutable = '"' + mutable + '"';
                }

                if (!string.IsNullOrEmpty(str)) str += " ";
                str += mutable;
            }

            return str;
        }


        /// <summary>
        /// Operator overload to append a single argument to the builder.
        /// </summary>
        /// <param name="a">The builder to which <paramref name="other"/> will be added.</param>
        /// <param name="other">The argument string to add.</param>
        /// <returns>The same <see cref="ArgumentBuilder"/> instance after adding the argument.</returns>
        public static ArgumentBuilder operator +(ArgumentBuilder a, string other) => a.Add(other);

        /// <summary>
        /// Operator overload to append multiple arguments to the builder.
        /// </summary>
        /// <param name="a">The builder to which the sequence will be added.</param>
        /// <param name="other">The sequence of argument strings to append.</param>
        /// <returns>The same <see cref="ArgumentBuilder"/> instance after adding the items.</returns>
        public static ArgumentBuilder operator +(ArgumentBuilder a, IEnumerable<string> other) => a.AddRange(other);
    }
}