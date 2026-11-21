using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// Tile endpoint of the asset overview's classic view: the workspace's assets as a
    /// card grid. The tile logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindTile"/>; this
    /// endpoint only scopes it to the asset kind. It is an independent sibling of the
    /// issue tile endpoint (not a subclass), so both keep their own route.
    /// </summary>
    [Title("kleenestar.core:object.tile.header")]
    [Cache]
    public sealed class Tile : global::KleeneStar.Core.WebRestApi.RestApiObjectKindTile
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Tile()
        {
        }

        /// <summary>
        /// Gets the object kind the tile view is scoped to: assets.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;

        /// <summary>
        /// Gets the key the user-defined quickfilters of the asset views are stored under. The
        /// bar of the tab is shared with the table view, so both read the same key.
        /// </summary>
        protected override string ViewKey => global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.KanbanQuickfilter.ViewKey;
    }
}
