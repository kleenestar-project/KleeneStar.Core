using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Work
{
    /// <summary>
    /// Sets the estimates and the milestone of an object: <c>POST /api/1/work/estimate</c> with
    /// <c>{ object, original, remaining, milestone }</c>. Empty estimates clear them; a milestone
    /// is sent only when it is to change (<c>"-"</c> takes the object off its milestone).
    /// </summary>
    /// <remarks>
    /// Planning an object is a change to it and asks for the right to change it.
    /// </remarks>
    [Title("kleenestar.core:planning.api.estimate.title")]
    [Cache]
    public sealed class Estimate : IRestApi
    {
        /// <summary>
        /// Handles <c>POST</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The figures, or why they were not stored.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
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

            var original = Durations.Parse(payload.Original);
            var remaining = Durations.Parse(payload.Remaining);

            if ((original is null && !string.IsNullOrWhiteSpace(payload.Original)) || (remaining is null && !string.IsNullOrWhiteSpace(payload.Remaining)))
            {
                return AutomationApi.Refuse("Write the estimates as durations, e.g. 2d or 4h.");
            }

            try
            {
                CoreHub.PlanningManager.SetEstimate(@object.Id, original, remaining);

                if (payload.Milestone is not null)
                {
                    CoreHub.PlanningManager.Assign(@object.Id, Guid.TryParse(payload.Milestone, out var milestoneId) ? milestoneId : null);
                }

                var estimate = CoreHub.PlanningManager.GetEstimate(@object.Id);

                return AutomationApi.Json(new { original = estimate.Original, remaining = estimate.Remaining, logged = estimate.Logged, milestoneId = estimate.MilestoneId });
            }
            catch (PlanningRefusedException refusal)
            {
                return WorkApi.Refuse(refusal);
            }
        }
    }
}
