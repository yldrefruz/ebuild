namespace ebuild.api
{
    public enum OptimizationLevel
    {
        None,     // No optimization (-O0 for GCC, /Od for MSVC)
        Size,     // Optimize for size (-Os for GCC, /O1 for MSVC)
        Speed,    // Optimize for speed (-O2 for GCC, /O2 for MSVC)
        Max       // Maximum optimization (-O3 for GCC, /Ox for MSVC)
    }
}