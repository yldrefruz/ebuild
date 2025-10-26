namespace ebuild.api
{
    /// <summary>
    /// Specifies the access limit category for items stored in an <see cref="AccessLimitList{T}"/>.
    /// </summary>
    public enum AccessLimit
    {
        /// <summary>
        /// Item is part of the public (exported/transitive) set.
        /// </summary>
        Public,

        /// <summary>
        /// Item is part of the private (non-transitive/local) set.
        /// </summary>
        Private
    }

    /// <summary>
    /// A small helper container that stores two lists of values (Public and Private) and
    /// provides convenience methods to add, enumerate and join them. This pattern is used
    /// throughout the build system to represent values that have public (propagated)
    /// and private (local) visibility.
    /// </summary>
    /// <typeparam name="T">The element type stored in the lists.</typeparam>
    public class AccessLimitList<T>
    {
        /// <summary>
        /// Publicly-visible items. These items are considered exported and will typically be
        /// propagated to dependents.
        /// </summary>
        public readonly List<T> Public = [];

        /// <summary>
        /// Private (non-exported) items. These items are local to the declaring module and
        /// are not automatically propagated.
        /// </summary>
        public readonly List<T> Private = [];

        /// <summary>
        /// Returns the list corresponding to the provided <paramref name="limit"/> value.
        /// If <paramref name="limit"/> is null the method returns a combined list containing
        /// both public and private items (equivalent to <see cref="Joined"/>).
        /// </summary>
        /// <param name="limit">The access limit to select. If null, both lists are returned joined.</param>
        /// <returns>
        /// The selected <see cref="List{T}"/>: either <see cref="Private"/>, <see cref="Public"/>,
        /// or a newly created list containing both when <paramref name="limit"/> is null.
        /// </returns>
        public List<T> GetLimited(AccessLimit? limit)
        {
            if (limit != null)
            {
                switch (limit)
                {
                    default:
                    case AccessLimit.Private:
                        return Private;
                    case AccessLimit.Public:
                        return Public;
                }
            }
            else
            {
                return Joined();
            }
        }

        /// <summary>
        /// Adds a value to the list associated with <paramref name="limit"/>.
        /// </summary>
        /// <param name="limit">The access limit category to which the value will be added.</param>
        /// <param name="value">The value to add.</param>
        public void Add(AccessLimit limit, T value)
        {
            switch (limit)
            {
                case AccessLimit.Public:
                    Public.Add(value);
                    break;
                default:
                case AccessLimit.Private:
                    Private.Add(value);
                    break;
            }
        }

        /// <summary>
        /// Adds a value to the private list. This is a convenience overload equivalent to
        /// calling <see cref="Add(AccessLimit, T)"/> with <see cref="AccessLimit.Private"/>.
        /// </summary>
        /// <param name="value">The value to add to the private list.</param>
        public void Add(T value)
        {
            Add(AccessLimit.Private, value);
        }


        /// <summary>
        /// Returns a new <see cref="List{T}"/> containing the public items followed by the private items.
        /// The returned list is a copy and can be modified without affecting the underlying <see cref="Public"/>
        /// and <see cref="Private"/> collections.
        /// </summary>
        /// <returns>A new list with public items first, then private items.</returns>
        public List<T> Joined()
        {
            List<T> all = new(Public);
            all.AddRange(Private);
            return all;
        }

        /// <summary>
        /// Adds the contents of <paramref name="enumerable"/> to the list identified by <paramref name="limit"/>.
        /// </summary>
        /// <param name="limit">The access limit category to which the sequence will be added.</param>
        /// <param name="enumerable">The sequence of items to add. The sequence is iterated and its items are appended.
        /// </param>
        public void AddRange(AccessLimit limit, IEnumerable<T> enumerable)
        {
            GetLimited(limit).AddRange(enumerable);
        }
    }
}