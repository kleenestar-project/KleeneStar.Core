using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Work
{
    /// <summary>
    /// The caller's own weekly capacity and load: <c>GET /api/1/work/capacity</c>, and
    /// <c>POST { capacity }</c> (a duration, e.g. <c>32h</c>) to change the capacity.
    /// </summary>
    /// <remarks>
    /// Everyone plans their own week; nobody sets another person's capacity here.
    /// </remarks>
    [Title("kleenestar.core:planning.api.capacity.title")]
    [Cache]
    public sealed class Capacity : IRestApi
    {
        /// <summary>
        /// Handles <c>GET</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The load, or <c>401</c> for nobody.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (callerId == Guid.Empty)
            {
                return new ResponseUnauthorized();
            }

            var load = CoreHub.PlanningManager.GetPersonalLoad(callerId, DateTime.UtcNow);

            return AutomationApi.Json(new
            {
                weeklyCapacity = load.WeeklyCapacity,
                remaining = load.RemainingMinutes,
                unestimated = load.Unestimated,
                loggedThisWeek = load.LoggedThisWeek,
                weeks = Math.Round(load.Weeks, 1),
                open = load.Open.Count
            });
        }

        /// <summary>
        /// Handles <c>POST</c>: sets the caller's weekly capacity.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c>, or why it was not set.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
        {
            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (callerId == Guid.Empty)
            {
                return new ResponseUnauthorized();
            }

            if (Durations.Parse(AutomationApi.Read<WorkApi.Payload>(request)?.Capacity) is not int minutes)
            {
                return AutomationApi.Refuse("Write the capacity as a duration, e.g. 32h.");
            }

            try
            {
                CoreHub.PlanningManager.SetWeeklyCapacity(callerId, minutes);

                return new ResponseNoContent();
            }
            catch (PlanningRefusedException refusal)
            {
                return WorkApi.Refuse(refusal);
            }
        }
    }
}
