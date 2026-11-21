using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// Table endpoint of the asset overview. The table logic — the column catalog, the
    /// per-identity layout, filtering, sorting and paging — lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindTable"/>; this
    /// endpoint only scopes it to the asset kind.
    /// </summary>
    /// <remarks>
    /// The table used to build a fixed set of four columns of its own. It now offers the
    /// same column catalog the issue table does — every field of every asset class of the
    /// workspace, with the visible set, order and widths stored per identity and per view.
    ///
    /// It contributes no row menu: the routes such a menu addresses (edit, clone, delete,
    /// favorite) exist for issues but not for assets, whose only per-object route is the
    /// reading view the row already links to. Adding those pages for assets is what would
    /// bring the menu, not a change here.
    /// </remarks>
    [Cache]
    public sealed class Table : global::KleeneStar.Core.WebRestApi.RestApiObjectKindTable
    {
        /// <summary>
        /// Gets the object kind the table is scoped to: assets.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;

        /// <summary>
        /// Gets the key the user-defined quickfilters of the asset views are stored under.
        /// It is the one the asset board's quickfilter bar writes, so a filter defined there
        /// narrows this table too.
        /// </summary>
        protected override string ViewKey => KanbanQuickfilter.ViewKey;
    }
}
