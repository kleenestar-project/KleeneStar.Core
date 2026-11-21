using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// The type-safe identity of the product backlog resource shared inside the scrum view's
    /// <see cref="WebExpress.WebApp.WebControl.ControlViewState"/>.
    /// </summary>
    /// <remarks>
    /// It is the backlog counterpart of <see cref="IssueSprintBoardResource"/>: both are
    /// declared by the same ViewState and map the same shared state onto their endpoint's
    /// query parameters, so the search and the quickfilter in the view header describe one
    /// filter that both views read — the board through the sprint Kanban endpoint, the
    /// backlog through the scrum backlog endpoint.
    /// </remarks>
    public sealed class IssueScrumBacklogResource : IDataResource
    {
    }
}
