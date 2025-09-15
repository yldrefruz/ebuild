namespace ebuild.api
{
    public interface IDependencyTree
    {
        public bool IsEmpty();
        public bool HasCircularDependency();
        public string GetCircularDependencyGraphString();

        public IEnumerable<IModuleFile> ToEnumerable(AccessLimit? accessLimit = null);
        /// <summary>
        /// Returns the first level dependencies and public dependencies of this module.
        /// These modules are generally what should be imported by the owner of this dependency tree.
        /// </summary>
        public IEnumerable<IModuleFile> GetFirstLevelAndPublicDependencies();

    }
}