using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WWW.Api._1_.Workflows
{
    /// <summary>
    /// Provides the read-only view of a workflow as a graph: its states as the nodes and its
    /// transitions as the edges. Backs the data service of the graph viewer, which queries
    /// <c>GET {uri}?id={workflowid}</c> once and renders the result.
    /// </summary>
    /// <remarks>
    /// The workflow rides along as the id query parameter rather than as a path segment, which is
    /// how <see cref="WorkflowEditor"/> addresses the same workflow; a second variable segment
    /// beside <c>_classid_</c> would make the route below <c>workflows</c> ambiguous.
    /// </remarks>
    [Title("Workflow graph")]
    [Cache]
    public sealed class Graph : RestApiGraph
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Graph()
        {
        }

        /// <summary>
        /// Retrieves the states of the workflow the request addresses.
        /// </summary>
        /// <remarks>
        /// The canvas position sits on the participation rather than on the status, because a
        /// status is defined per class and can take part in several workflows with a different
        /// layout in each.
        /// </remarks>
        /// <param name="request">The current request.</param>
        /// <returns>
        /// The nodes of the graph. An unknown or unparsable workflow yields an empty result, which
        /// renders an empty canvas rather than an error.
        /// </returns>
        protected override IEnumerable<RestApiGraphNode> RetrieveNodes(IRequest request)
        {
            if (!TryGetWorkflowId(request, out var workflowId))
            {
                return [];
            }

            using var db = ModelHub.CreateDbContext();
            var workflow = db.Workflows
                .Include(w => w.WorkflowStatuses)
                    .ThenInclude(ws => ws.Status)
                        .ThenInclude(s => s.Category)
                .AsNoTracking()
                .FirstOrDefault(w => w.Id == workflowId);

            if (workflow?.WorkflowStatuses is null)
            {
                return [];
            }

            var participations = workflow.WorkflowStatuses
                .Where(ws => ws.Status is not null && ws.Status.State == StatusState.Active)
                .ToList();

            // a workflow that has never been through the designer carries every state at the
            // origin, which would stack the nodes on top of each other. Withholding the positions
            // hands the placement to the layout simulation instead, which is the only way such a
            // workflow reads at all; a workflow that was laid out delivers every position and the
            // simulation then has nothing to place and leaves the authored layout alone.
            var laidOut = participations.Any(ws => ws.X != 0 || ws.Y != 0);

            return participations
                .Select(ws => new RestApiGraphNode()
                {
                    Id = ws.Status.Id.ToString(),
                    Label = ws.Status.Name,
                    BackgroundColor = ws.Status.Category?.Color ?? "#6c757d",
                    ForegroundColor = "#ffffff",
                    // the status symbol is a picture, so it belongs in Image; a URL placed in
                    // Icon would be set as a CSS class on an empty element and render nothing
                    Image = ws.Status.Icon?.Uri?.ToString(),
                    // the viewer knows no entry or terminal state, so the shape carries the mark:
                    // the round terminator against the rectangular step is how a state machine is
                    // read, and the label moves out of the circle to stay legible
                    Shape = ws.IsStart || ws.IsEnd ? "circle" : null,
                    Layout = ws.IsStart || ws.IsEnd ? "label-below" : null,
                    X = laidOut ? ws.X : null,
                    Y = laidOut ? ws.Y : null
                })
                .ToList();
        }

        /// <summary>
        /// Retrieves the transitions of the workflow the request addresses.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <returns>
        /// The edges of the graph. A transition whose endpoint is not among the delivered states -
        /// an archived status, for instance - is dropped by the client rather than drawn to nowhere.
        /// </returns>
        protected override IEnumerable<RestApiGraphEdge> RetrieveEdges(IRequest request)
        {
            if (!TryGetWorkflowId(request, out var workflowId))
            {
                return [];
            }

            using var db = ModelHub.CreateDbContext();
            var transitions = db.Transitions
                .Where(t => t.WorkflowId == workflowId && t.State == TransitionState.Active)
                .AsNoTracking()
                .ToList();

            return transitions
                .Select(t => new RestApiGraphEdge()
                {
                    Id = t.Id.ToString(),
                    From = t.SourceId.ToString(),
                    To = t.TargetId.ToString(),
                    Label = t.Name,
                    Color = t.Color,
                    DashArray = t.DashArray,
                    Waypoints = t.Waypoints?
                        .Select(w => new RestApiGraphWaypoint() { X = w.X, Y = w.Y })
                        .ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Reads the workflow the request addresses from its id parameter.
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <param name="workflowId">
        /// When this method returns, contains the addressed workflow, or <see cref="Guid.Empty"/>
        /// when the request carries no usable id.
        /// </param>
        /// <returns>True when the request addresses a workflow.</returns>
        private static bool TryGetWorkflowId(IRequest request, out Guid workflowId)
        {
            return Guid.TryParse(request?.GetParameter<ParameterId>()?.Value, out workflowId);
        }
    }
}
