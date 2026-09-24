using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a condition that determines whether the workspace overview has nothing to show.
    /// </summary>
    /// <remarks>
    /// It gates the empty-state placeholder against <see cref="WorkspaceNotEmptyStateCondition"/>,
    /// which gates the view. The two are exact complements, so the page always shows one of them
    /// and never both.
    /// </remarks>
    internal class WorkspaceEmptyStateCondition : ICondition
    {
        /// <summary>
        /// Determines whether no workspace exists that the caller may read - the overview lists
        /// only those (<see cref="WebPermission.ContentVisibility"/>), so an installation full
        /// of workspaces the caller may not see is empty for them.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>True when the overview has no workspace to list.</returns>
        public bool Fulfillment(IRequest request)
        {
            // only the existence matters, so the query stops after the first hit
            var query = new Query<Model.Entities.Workspace>()
                .WithPaging(0, 1);

            return !CoreHub.WorkspaceManager
                .GetWorkspaces(WebPermission.ContentVisibility.Restrict(query))
                .Any();
        }
    }
}
