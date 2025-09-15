using System.Diagnostics.CodeAnalysis;

namespace ebuild.api
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public abstract class PlatformBase(string name)
    {
        public readonly string Name = name;
        public abstract string? GetDefaultToolchainName();




        public virtual string ExtensionForCompiledSourceFile => ".obj";
        public virtual bool SupportsDebugFiles => true;
        public virtual string ExtensionForCompiledSourceFile_DebugFile => ".pdb";
        public abstract string ExtensionForStaticLibrary { get; }
        public abstract string ExtensionForSharedLibrary { get; }
        public abstract string ExtensionForExecutable { get; }

        public virtual IEnumerable<string> GetPlatformIncludes(ModuleBase module)
        {
            yield break;
        }
        public virtual IEnumerable<string> GetPlatformLibrarySearchPaths(ModuleBase module)
        {
            yield break;
        }
        public virtual IEnumerable<string> GetPlatformLibraries(ModuleBase module)
        {
            yield break;
        }
        public virtual IEnumerable<string> GetPlatformLinkerFlags(ModuleBase module)
        {
            yield break;
        }
        public virtual IEnumerable<string> GetPlatformCompilerFlags(ModuleBase module)
        {
            yield break;
        }
        public virtual IEnumerable<Definition> GetPlatformDefinitions(ModuleBase module)
        {
            yield break;
        }
    }
}