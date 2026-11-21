using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// The type-safe identity of the issue Kanban board resource shared inside the Kanban
    /// tab's <see cref="WebExpress.WebApp.WebControl.ControlViewState"/>.
    /// </summary>
    /// <remarks>
    /// The board renders it, and the search and the quickfilter of the tab header write into
    /// the state it maps onto the endpoint's <c>q</c> and <c>f</c> parameters — so a chip or
    /// a search term re-queries the board without a bind wire, which the Kanban control has
    /// none of. All three resolve the ViewState by this type rather than by ancestry, so
    /// they may sit in sibling fragments.
    /// </remarks>
    public sealed class IssueKanbanResource : IDataResource
    {
    }
}
