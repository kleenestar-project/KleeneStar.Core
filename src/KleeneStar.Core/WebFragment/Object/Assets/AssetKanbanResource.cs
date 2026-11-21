using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// The type-safe identity of the asset Kanban board resource shared inside the Kanban
    /// tab's <see cref="WebExpress.WebApp.WebControl.ControlViewState"/>.
    /// </summary>
    /// <remarks>
    /// The asset counterpart of
    /// <see cref="Issues.IssueKanbanResource"/>: the board renders it while the search and
    /// the quickfilter of the tab header write into the state it maps onto the endpoint's
    /// <c>q</c> and <c>f</c> parameters. It is a type of its own so the two boards keep
    /// separate state on pages that carry both.
    /// </remarks>
    public sealed class AssetKanbanResource : IDataResource
    {
    }
}
