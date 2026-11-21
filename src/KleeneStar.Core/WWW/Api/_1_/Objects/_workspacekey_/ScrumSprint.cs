using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// REST API scrum sprint endpoint for the objects of a workspace. Returns the
    /// active sprint overview — name, goal, timeframe, points progress and burn-down —
    /// built by the base class from the sprints and items of the workspace.
    /// </summary>
    [Title("kleenestar.core:object.view.scrum.sprint.title")]
    [Cache]
    public sealed class ScrumSprint : RestApiScrumSprint<Sprint, Model.Entities.Object>
    {
        /// <summary>
        /// Returns a <see cref="KleeneStarDbContext"/> so the managers can run their
        /// queries against the real database.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the sprints of the workspace addressed by the request route.
        /// </summary>
        /// <param name="query">The query criteria (unused; the route scopes the set).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The sprints of the workspace.</returns>
        protected override IEnumerable<Sprint> RetrieveSprints(IQuery<Sprint> query, IQueryContext context, IRequest request)
        {
            var workspace = ScrumProjection.GetWorkspace(request);

            return workspace is null
                ? []
                : CoreHub.SprintManager.GetSprintsForWorkspace(workspace.Id);
        }

        /// <summary>
        /// Returns the active objects of the workspace addressed by the request route.
        /// </summary>
        /// <param name="query">The query criteria (unused; the route scopes the set).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The active objects of the workspace.</returns>
        protected override IEnumerable<Model.Entities.Object> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return ScrumProjection.GetItems(request);
        }

        /// <summary>
        /// Converts a sprint entity into the REST sprint DTO.
        /// </summary>
        /// <param name="sprint">The sprint entity.</param>
        /// <returns>The REST sprint DTO.</returns>
        protected override RestApiScrumSprintItem ToRestSprint(Sprint sprint)
        {
            return ScrumProjection.ToRestSprint(sprint);
        }

        /// <summary>
        /// Converts an object entity into the REST item DTO.
        /// </summary>
        /// <param name="item">The object entity.</param>
        /// <returns>The REST item DTO.</returns>
        protected override RestApiScrumItem ToRestItem(Model.Entities.Object item)
        {
            return ScrumProjection.ToRestItem(item);
        }
    }
}
