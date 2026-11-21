using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a condition that determines whether the workspace overview has something to show.
    /// </summary>
    /// <remarks>
    /// It is the exact complement of <see cref="WorkspaceEmptyStateCondition"/> and is derived from
    /// it rather than repeating the query, so the two can never disagree and leave the page with
    /// both the view and the empty-state placeholder, or with neither.
    /// </remarks>
    internal class WorkspaceNotEmptyStateCondition : ICondition
    {
        private readonly WorkspaceEmptyStateCondition _empty = new();

        /// <summary>
        /// Determines whether at least one workspace exists.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>True when the overview has a workspace to list.</returns>
        public bool Fulfillment(IRequest request)
        {
            return !_empty.Fulfillment(request);
        }
    }
}
