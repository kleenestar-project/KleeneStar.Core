using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Tile endpoint of the issue overview's classic view: the workspace's issues as a
    /// card grid. The tile logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindTile"/>; this
    /// endpoint only scopes it to the issue kind.
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
        /// Gets the object kind the tile view is scoped to: issues.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Gets the key the user-defined quickfilters of the issue views are stored under. The
        /// bar of the tab is shared with the table view, so both read the same key.
        /// </summary>
        protected override string ViewKey => global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Quickfilter.ViewKey;
    }
}
