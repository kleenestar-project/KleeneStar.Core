using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// The type-safe identity of the active-sprint Kanban board resource shared inside the
    /// sprint tab's <see cref="WebExpress.WebApp.WebControl.ControlViewState"/>. The
    /// ViewState declares this resource (backed by the sprint Kanban endpoint) and both the
    /// quickfilter (which writes the active filter into the shared state) and the Kanban
    /// board (which renders the resource) bind to it by type — so a chip selection re-queries
    /// the board without a <c>BindFilter</c> wire (the Kanban control has no filter binding).
    /// </summary>
    public sealed class IssueSprintBoardResource : IDataResource
    {
    }
}
