using KleeneStar.Model.Entities;
using System;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Validates the tab that owns a board configuration against its route and board type.
    /// </summary>
    public static class BoardViewScope
    {
        /// <summary>
        /// Resolves a workspace board tab, retaining the legacy board for requests without a tab.
        /// </summary>
        /// <param name="request">The request carrying the optional tab parameter.</param>
        /// <param name="workspaceId">The workspace identified by the route.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="type">The required tab type.</param>
        /// <returns>The validated tab identifier, or an empty identifier for the legacy board.</returns>
        public static Guid Workspace(IRequest request, Guid workspaceId, string kind, ObjectViewType type)
        {
            var id = Parse(request);
            if (id == Guid.Empty)
            {
                return id;
            }

            var view = CoreHub.ObjectViewManager.GetObjectView(id);
            if (view is null || view.WorkspaceId != workspaceId || view.Kind != kind
                || view.State != ObjectViewState.Active
                || (view.ViewType != type && !(type == ObjectViewType.ScrumSprint && view.ViewType == ObjectViewType.ScrumBacklog)))
            {
                throw new RestApiRefusal("The board tab does not belong to this overview.");
            }

            return id;
        }

        /// <summary>
        /// Resolves an insight board tab, retaining the legacy board for requests without a tab.
        /// </summary>
        /// <param name="request">The request carrying the optional tab parameter.</param>
        /// <param name="insightId">The insight identified by the route.</param>
        /// <param name="type">The required tab type key.</param>
        /// <returns>The validated tab identifier, or an empty identifier for the legacy board.</returns>
        public static Guid Insight(IRequest request, Guid insightId, string type)
        {
            var id = Parse(request);
            if (id == Guid.Empty)
            {
                return id;
            }

            var view = CoreHub.InsightManager.GetView(id);
            if (view is null || view.InsightId != insightId || view.ViewType != type || view.State != ObjectViewState.Active)
            {
                throw new RestApiRefusal("The board tab does not belong to this insight.");
            }

            return id;
        }

        /// <summary>
        /// Parses the optional tab identifier and refuses malformed or explicitly empty identifiers.
        /// </summary>
        /// <param name="request">The request carrying the optional tab parameter.</param>
        /// <returns>The tab identifier, or an empty identifier when the parameter is absent.</returns>
        private static Guid Parse(IRequest request)
        {
            var parameter = request?.GetParameter("v");
            if (parameter is null)
            {
                return Guid.Empty;
            }

            if (!Guid.TryParse(parameter.Value, out var id) || id == Guid.Empty)
            {
                throw new RestApiRefusal("The board tab identifier is invalid.");
            }

            return id;
        }
    }
}
