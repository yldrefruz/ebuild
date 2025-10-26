namespace ebuild.api.Linker
{
    /// <summary>
    /// Abstract base class for linker implementations.
    ///
    /// Concrete linkers perform the final linking step that consumes object files and
    /// produces an output artifact (static library, shared library or executable) according
    /// to the provided <see cref="LinkerSettings"/>.
    /// </summary>
    public abstract class LinkerBase
    {
        /// <summary>
        /// Link inputs specified by <paramref name="settings"/> into the requested output
        /// artifact. Implementations should report diagnostics and respect the
        /// <paramref name="cancellationToken"/>.
        /// </summary>
        /// <param name="settings">Linker settings that list inputs, output path and flags.</param>
        /// <param name="cancellationToken">Token used to cancel the linking operation.</param>
        /// <returns><c>true</c> when linking succeeded; otherwise <c>false</c>.</returns>
        public abstract Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken = default);
    }
}