using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WWW.Api._1_.SlaClocks._objectkey_
{
    /// <summary>
    /// REST endpoint serving the running clock of one SLA target on one object. The URL is
    /// <c>/api/1/slaclocks/{objectkey}?slatargetid={slatargetid}</c>; the <c>{objectkey}</c>
    /// URL segment is declared via <see cref="ObjectKeySegmentAttribute"/>.
    /// </summary>
    /// <remarks>
    /// It backs the <see cref="WebExpress.WebApp.WebControl.ControlDataSla"/> tiles hosted by
    /// <see cref="WebFragment.Object.Issues.IssueSlaCardFragment"/>: <c>GET {base}</c> answers
    /// with the state the widget adopts — <c>status</c>, <c>target</c>, <c>elapsed</c>,
    /// <c>remaining</c>, <c>period</c>, <c>cycle</c>, <c>cycles</c>, <c>paused</c> and
    /// <c>settled</c>. One target is addressed per call, because one widget shows one
    /// agreement.
    /// <para>
    /// The endpoint owns no logic of its own: it derives the clock through
    /// <see cref="SlaClock"/> and reports what <see cref="SlaEvaluator"/> makes of it, so the
    /// card's first paint and every later poll arrive at the same status by the same route.
    /// </para>
    /// <para>
    /// There is no <c>POST</c> counterpart to the tutorial's demo endpoint. A pause, a resume
    /// or a manual settlement would have to be written somewhere, and <b>KleeneStar</b> stores
    /// policies rather than per-object timers — the clock is derived from the object's
    /// workflow status instead (see <see cref="SlaClock"/>). The card therefore renders its
    /// tiles without actions; the way to stop an agreement is to move the ticket into one of
    /// the policy's pause statuses.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.sla.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Serialization options for the clock payload: camelCase property names, matching
        /// the state shape the client widget reads.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET {base}</c>: returns the state of the addressed target's clock on the
        /// object addressed by the URL <c>{objectkey}</c> segment.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>
        /// The state JSON, <c>400</c> when the target id is missing or malformed, or
        /// <c>404</c> when the object does not exist or the target is not one of the active
        /// policies of the object's class.
        /// </returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var keyParameter = request?.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            if (!Guid.TryParse(request?.GetParameter<SlaTargetIdParameter>()?.Value, out var targetId))
            {
                return Error($"The query parameter '{SlaTargetIdParameter.Key}' is missing or is not a valid id.");
            }

            // the lookup runs over the active policies of the object's class that cover the
            // object, so a target of a draft, retired or foreign policy - or of one whose scope
            // leaves this object out - is not reachable through this route
            var policy = SlaScope
                .Select
                (
                    CoreHub.SlaManager.GetSlas(@object.ClassId).Where(p => p.State == SlaPolicyState.Active),
                    @object,
                    CoreHub.FieldManager,
                    CoreHub.ValueManager,
                    CoreHub.PriorityManager,
                    CoreHub.ObjectTagManager
                )
                .FirstOrDefault(p => (p.Targets ?? []).Any(t => t.Id == targetId));

            var target = policy?.Targets.FirstOrDefault(t => t.Id == targetId);

            if (target is null)
            {
                return new ResponseNotFound();
            }

            var status = SlaClock.ResolveStatus(@object, CoreHub.FieldManager, CoreHub.ValueManager, CoreHub.WorkflowManager);
            var moment = DateTime.Now;
            var evaluation = SlaEvaluator.Evaluate(SlaClock.Derive(@object, policy, target, status, moment), moment);

            return Json(new
            {
                status = evaluation.Status.ToValue(),
                target = (long)evaluation.Budget.TotalSeconds,
                elapsed = (long)evaluation.Elapsed.TotalSeconds,
                remaining = (long)evaluation.Remaining.TotalSeconds,
                period = (long)evaluation.Period.TotalSeconds,
                cycle = evaluation.Cycle,
                cycles = evaluation.Cycles,
                paused = evaluation.IsPaused,
                settled = evaluation.IsSettled
            });
        }

        /// <summary>
        /// Wraps a payload into a JSON <c>200</c> response.
        /// </summary>
        /// <param name="payload">The payload to serialize.</param>
        /// <returns>The response.</returns>
        private static IResponse Json(object payload)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Wraps an error message into a JSON <c>400</c> response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The response.</returns>
        private static IResponse Error(string message)
        {
            return new ResponseBadRequest
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }
    }
}
