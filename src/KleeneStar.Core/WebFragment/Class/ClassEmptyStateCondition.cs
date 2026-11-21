using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a condition that determines whether the class overview of the workspace
    /// addressed by the route has nothing to show.
    /// </summary>
    /// <remarks>
    /// It gates the empty-state placeholder against <see cref="ClassNotEmptyStateCondition"/>,
    /// which gates the view. The two are exact complements, so the page always shows one of them
    /// and never both. Unlike the workspace overview the question is asked per workspace, because
    /// the page lists the classes of a single one.
    /// </remarks>
    internal class ClassEmptyStateCondition : ICondition
    {
        /// <summary>
        /// Determines whether the addressed workspace has no class.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>
        /// True when the overview has no class to list, which includes a route that does not
        /// address an existing workspace.
        /// </returns>
        public bool Fulfillment(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);

            // an unknown workspace lists nothing, which is the empty state rather than the view
            var query = new Query<Model.Entities.Class>()
                .WhereEquals(x => x.WorkspaceId, workspace?.Id ?? Guid.Empty)
                .WithPaging(0, 1);

            return !CoreHub.ClassManager
                .GetClasses(query)
                .Any();
        }
    }
}
