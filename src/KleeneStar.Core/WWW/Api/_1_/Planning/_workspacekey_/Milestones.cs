using KleeneStar.Core.WebManager;
using KleeneStar.Core.WWW.Api._1_.Automation;
using KleeneStar.Core.WWW.Api._1_.Work;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Planning._workspacekey_
{
    /// <summary>
    /// Adds or changes a milestone of a workspace: <c>POST /api/1/planning/{workspacekey}/milestones</c>
    /// with <c>{ id, name, description, due, state }</c>.
    /// </summary>
    [Title("kleenestar.core:planning.api.milestones.title")]
    [Cache]
    public sealed class Milestones : IRestApi
    {
        /// <summary>
        /// Handles <c>POST</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The milestone, or why it was not stored.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var payload = AutomationApi.Read<MilestonePayload>(request);

            if (payload is null)
            {
                return AutomationApi.Refuse("The request body must describe a milestone.");
            }

            var id = AutomationApi.ParseId(payload.Id);
            var milestone = id is { } milestoneId ? CoreHub.PlanningManager.GetMilestone(milestoneId) : new Milestone { WorkspaceId = workspace.Id };

            if (milestone is null || milestone.WorkspaceId != workspace.Id)
            {
                return new ResponseNotFound();
            }

            DateTime? due = null;

            if (!string.IsNullOrWhiteSpace(payload.Due))
            {
                if (!WorkApi.TryParseMoment(payload.Due, out var moment))
                {
                    return AutomationApi.Refuse("Write the due date as a date.");
                }

                due = moment;
            }

            milestone.Name = payload.Name;
            milestone.Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim();
            milestone.Due = due;
            milestone.State = Enum.TryParse<MilestoneState>(payload.State, true, out var state) ? state : MilestoneState.Open;

            try
            {
                CoreHub.PlanningManager.SaveMilestone(milestone);

                return AutomationApi.Json(new { id = milestone.Id, name = milestone.Name, due = milestone.Due, state = milestone.State.ToString().ToLowerInvariant() });
            }
            catch (PlanningRefusedException refused)
            {
                return WorkApi.Refuse(refused);
            }
        }

        /// <summary>
        /// The body of a milestone.
        /// </summary>
        internal sealed class MilestonePayload
        {
            /// <summary>Gets or sets the id of the milestone to change, or empty for a new one.</summary>
            public string Id { get; set; }

            /// <summary>Gets or sets the name.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the description.</summary>
            public string Description { get; set; }

            /// <summary>Gets or sets the due date.</summary>
            public string Due { get; set; }

            /// <summary>Gets or sets the state: open, reached or archived.</summary>
            public string State { get; set; }
        }
    }
}
