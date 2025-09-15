namespace ebuild.api.Compiler
{
    public abstract class CompilerBase
    {
        /// <summary>
        /// Generate the "thing" that we are asked for.
        /// </summary>
        /// <param name="type">the type of the "thing" we are asked for. For example can be <code>GenerateCompileCommands</code> for creating compile_commands.json</param>
        /// <param name="data">the additional data to use</param>
        /// <returns>whether the generation was successful.</returns>
        public abstract Task<bool> Generate(CompilerSettings settings, CancellationToken cancellationToken, string type, object? data = null);
        /// <summary>
        /// Compile the module.
        /// </summary>
        /// <returns>Whether the compilation was successful.</returns>
        public abstract Task<bool> Compile(CompilerSettings settings, CancellationToken cancellationToken);
    }
}