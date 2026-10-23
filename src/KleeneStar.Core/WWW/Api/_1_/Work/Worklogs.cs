using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Work
{
    /// <summary>
    /// The time logged on an object: <c>GET /api/1/work/worklogs?object=KEY</c> answers the
    /// estimates, the total and the entries the caller may see; <c>POST</c>
    /// <c>{ object, started, duration, comment, remaining }</c> logs work.
    /// </summary>
    /// <remarks>
    /// A worklog is personal data: every reader of the object sees the totals, the entries are
    /// shown to their author and to the administrators of the workspace. Logging work needs
    /// the right to change the object and time tracking switched on in its workspace.
    /// </remarks>
    [Title("kleenestar.core:planning.api.worklogs.title")]
    [Cache]
    public sealed class Worklogs : IRestApi
    {
        /// <summary>
        /// Handles <c>GET</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The figures, or <c>404</c> for an object the caller may not read.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request.GetParameter("object")?.Value ?? string.Empty);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var administrator = ContentAuthorization.MayAdministerWorkspace(@object.WorkspaceId, request);
            var estimate = CoreHub.PlanningManager.GetEstimate(@object.Id);

            return AutomationApi.Json(new
            {
                timeTracking = CoreHub.PlanningManager.IsTimeTracking(@object.WorkspaceId),
                original = estimate.Original,
                remaining = estimate.Remaining,
                logged = estimate.Logged,
                milestoneId = estimate.MilestoneId,
                worklogs = CoreHub.PlanningManager.GetWorklogs(@object.Id)
                    .Where(x => administrator || x.IdentityId == callerId)
                    .Select(x => new
                    {
                        id = x.Id,
                        by = Approvals.ApprovalApi.NameOf(x.IdentityId),
                        started = x.Started,
                        minutes = x.Minutes,
                        duration = Durations.Format(x.Minutes),
                        comment = x.Comment
                    })
            });
        }

        /// <summary>
        /// Handles <c>POST</c>: logs work.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The worklog, or why it was not logged.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Log(IRequest request)
        {
            var payload = AutomationApi.Read<WorkApi.Payload>(request);
            var @object = string.IsNullOrWhiteSpace(payload?.Object) ? null : CoreHub.ObjectManager.GetObjectByKey(payload.Object);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            if (!ContentAuthorization.MayWrite(@object, request))
            {
                return new ResponseForbidden();
            }

            if (Durations.Parse(payload.Duration) is not int minutes)
            {
                return AutomationApi.Refuse("Write the time spent as a duration, e.g. 1h 30m.");
            }

            int? remaining = null;

            if (!string.IsNullOrWhiteSpace(payload.Remaining))
            {
                remaining = Durations.Parse(payload.Remaining);

                if (remaining is null)
                {
                    return AutomationApi.Refuse("Write the remaining estimate as a duration, e.g. 4h.");
                }
            }

            if (!WorkApi.TryParseMoment(payload.Started, out var started))
            {
                return AutomationApi.Refuse("Write when the work began as a date.");
            }

            try
            {
                var worklog = CoreHub.PlanningManager.Log(@object.Id, CoreHub.SessionManager.GetCurrentIdentityId(request), started, minutes, payload.Comment, remaining);

                return AutomationApi.Json(new { id = worklog.Id, minutes = worklog.Minutes, started = worklog.Started });
            }
            catch (PlanningRefusedException refusal)
            {
                return WorkApi.Refuse(refusal);
            }
        }
    }
}
