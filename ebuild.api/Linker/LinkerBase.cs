namespace ebuild.api.Linker
{
    public abstract class LinkerBase
    {
        /// <summary>
        /// Link the module.
        /// </summary>
        /// <returns>Whether the linking was successful.</returns>
        public abstract Task<bool> Link(LinkerSettings settings, CancellationToken cancellationToken = default);
    }
}