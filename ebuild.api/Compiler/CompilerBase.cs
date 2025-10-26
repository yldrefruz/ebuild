namespace ebuild.api.Compiler
{
    /// <summary>
    /// Abstract base class for compiler implementations.
    ///
    /// Concrete compilers implement the compilation and optional generation steps used by the
    /// build system (for example producing object files or generating auxiliary files such as
    /// a compile_commands.json). Implementations should interpret the provided
    /// <see cref="CompilerSettings"/> and use the cancellation token to abort long-running
    /// operations promptly.
    /// </summary>
    public abstract class CompilerBase
    {
        /// <summary>
        /// Generate an auxiliary artifact or perform a generation step for the compiler.
        /// Examples of generation types include creating IDE helper files such as
        /// <c>GenerateCompileCommands</c> which can produce a compile_commands.json.
        /// </summary>
        /// <param name="settings">Compilation and generation settings describing inputs and outputs.</param>
        /// <param name="cancellationToken">Token used to cancel the generation operation.</param>
        /// <param name="type">A string identifier indicating which generation action is requested.</param>
        /// <param name="data">Optional additional data required for the generation action.</param>
        /// <returns><c>true</c> when the generation completed successfully; otherwise <c>false</c>.</returns>
        public abstract Task<bool> Generate(CompilerSettings settings, CancellationToken cancellationToken, string type, object? data = null);

        /// <summary>
        /// Compiles the sources described by <paramref name="settings"/> producing the output
        /// specified in the settings (for example an object file). Implementations should
        /// surface compilation diagnostics via logging and respect the provided
        /// <paramref name="cancellationToken"/>.
        /// </summary>
        /// <param name="settings">Compilation settings that specify sources, target architecture,
        /// output paths and flags.</param>
        /// <param name="cancellationToken">Token used to cancel the compilation operation.</param>
        /// <returns><c>true</c> when the compilation succeeded; otherwise <c>false</c>.</returns>
        public abstract Task<bool> Compile(CompilerSettings settings, CancellationToken cancellationToken);
    }
}