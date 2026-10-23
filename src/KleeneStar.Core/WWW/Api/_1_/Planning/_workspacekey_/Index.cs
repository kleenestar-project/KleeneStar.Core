using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Planning._workspacekey_
{
    /// <summary>
    /// The planning of a workspace: <c>GET /api/1/planning/{workspacekey}</c> answers whether time
    /// is tracked, the team's load and the milestones with their progress; <c>POST</c>
    /// <c>{ timeTracking: "true" | "false" }</c> switches time tracking.
    /// </summary>
    /// <remarks>
    /// The team's load holds what each person logged this week - personal data a workspace's
    /// administrators plan with and its other readers do not see - so the endpoint is workspace
    /// administration, like the page.
    /// </remarks>
    [Title("kleenestar.core:planning.api.title")]
    [WorkspaceKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Handles <c>GET</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The planning of the workspace.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var now = DateTime.UtcNow;

            return AutomationApi.Json(new
            {
                timeTracking = CoreHub.PlanningManager.IsTimeTracking(workspace.Id),
                team = CoreHub.PlanningManager.GetTeamLoad(workspace.Id, now).Select(x => new
                {
                    identityId = x.IdentityId,
                    name = Approvals.ApprovalApi.NameOf(x.IdentityId),
                    open = x.Open.Count,
                    remaining = x.RemainingMinutes,
                    unestimated = x.Unestimated,
                    loggedThisWeek = x.LoggedThisWeek,
                    weeklyCapacity = x.WeeklyCapacity,
                    weeks = Math.Round(x.Weeks, 1)
                }),
                milestones = CoreHub.PlanningManager.GetMilestones(workspace.Id).Select(x =>
                {
                    var progress = CoreHub.PlanningManager.GetProgress(x.Id);

                    return new
                    {
                        id = x.Id,
                        name = x.Name,
                        description = x.Description,
                        due = x.Due,
                        state = x.State.ToString().ToLowerInvariant(),
                        total = progress.Total,
                        done = progress.Done,
                        hidden = progress.Hidden,
                        percent = progress.Percent,
                        remaining = progress.RemainingMinutes,
                        logged = progress.LoggedMinutes
                    };
                })
            });
        }

        /// <summary>
        /// Handles <c>POST</c>: switches time tracking.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c>, or the refusal.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var payload = AutomationApi.Read<SettingsPayload>(request);

            if (!bool.TryParse(payload?.TimeTracking, out var enabled))
            {
                return AutomationApi.Refuse("Say whether time is tracked: true or false.");
            }

            CoreHub.PlanningManager.SetTimeTracking(workspace.Id, enabled);

            return new ResponseNoContent();
        }

        /// <summary>
        /// The body of the settings.
        /// </summary>
        internal sealed class SettingsPayload
        {
            /// <summary>Gets or sets whether time is tracked.</summary>
            public string TimeTracking { get; set; }
        }
    }
}
