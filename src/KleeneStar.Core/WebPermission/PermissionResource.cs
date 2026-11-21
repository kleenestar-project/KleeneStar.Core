namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Names one resource a permission is evaluated against: the kind of thing it is, and which
    /// one.
    /// </summary>
    /// <remarks>
    /// A check is issued against a <i>chain</i> of these rather than against a single resource,
    /// because a grant does not have to sit on the record being touched to govern it: a grant on
    /// a workspace governs everything filed in it, and a grant on a class governs the objects of
    /// that class. The caller states the chain from the most specific resource to the most
    /// general, and the evaluation reads it in that order.
    /// </remarks>
    /// <param name="Scope">The kind of resource, as named in <see cref="PermissionScope"/>.</param>
    /// <param name="ScopeId">The identifier of the resource within its scope.</param>
    public readonly record struct PermissionResource(string Scope, string ScopeId)
    {
        /// <summary>
        /// Gets a value indicating whether the resource is addressable at all. A chain link that
        /// could not be resolved - an object whose workspace is gone, a route naming nothing - is
        /// skipped rather than treated as an ungranted resource.
        /// </summary>
        public bool IsResolved => !string.IsNullOrWhiteSpace(Scope) && !string.IsNullOrWhiteSpace(ScopeId);
    }
}
