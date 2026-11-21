using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// List endpoint of the asset overview's classic view: the workspace's assets as a
    /// vertical frame list. The list logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindList"/>; this
    /// endpoint only scopes it to the asset kind. It is an independent sibling of the
    /// issue list endpoint (not a subclass), so both keep their own route.
    /// </summary>
    [Title("kleenestar.core:object.list.header")]
    [Cache]
    public sealed class List : global::KleeneStar.Core.WebRestApi.RestApiObjectKindList
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public List()
        {
        }

        /// <summary>
        /// Gets the object kind the list is scoped to: assets.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;

        /// <summary>
        /// Gets the key the user-defined quickfilters of the asset views are stored under. The
        /// bar of the tab is shared with the table view, so both read the same key.
        /// </summary>
        protected override string ViewKey => global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.KanbanQuickfilter.ViewKey;
    }
}
