using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System;
using System.Globalization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Transitions._objectkey_
{
    /// <summary>
    /// REST endpoint backing the state picker on the object's workflow card
    /// (<see cref="WebFragment.Object.ObjectPropertyWorkflowCardFragment"/>). The URL is
    /// <c>/api/1/transitions/{objectkey}?fieldid={fieldid}&amp;stateid={stateid}</c>; the
    /// <c>{objectkey}</c> URL segment is declared via <see cref="ObjectKeySegmentAttribute"/>
    /// so callers can bind it from an <see cref="ObjectKeyParameter"/>.
    /// </summary>
    /// <remarks>
    /// A <c>GET</c> moves the addressed object's workflow-backed field to the requested state
    /// and then issues a <c>302</c> redirect back to the object detail page, so a plain
    /// navigation link inside a dropdown can drive the change without any client-side
    /// scripting — the same shape the assignee toggle uses. The state machine itself is
    /// enforced by <see cref="IWorkflowManager.ExecuteTransition"/>: this endpoint only
    /// translates the request into that call and turns its outcome into a toast.
    /// </remarks>
    [Title("kleenestar.core:object.transition.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET {base}</c>: moves the addressed object's workflow field to the
        /// requested state and redirects to the object detail page.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>A <c>302</c> redirect to the object detail page.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Execute(IRequest request)
        {
            var keyParameter = request?.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParameter?.Value);

            // a caller who may not move the object is sent back to it unchanged, the silence a
            // refused move leaves anyway
            if (@object is not null &&
                global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(@object, request, typeof(global::KleeneStar.Core.WebPermissions.TransitionExecutePermission)) &&
                Guid.TryParse(request?.GetParameter<FieldIdParameter>()?.Value, out var fieldId) &&
                Guid.TryParse(request?.GetParameter<WorkflowStateIdParameter>()?.Value, out var stateId))
            {
                var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
                var result = CoreHub.WorkflowManager.ExecuteTransition(@object.Id, fieldId, stateId, identityId);

                Report(result);
            }

            // dispatch to the detail view matching the object's kind (/issue, /document, …)
            var target = global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                .ResolveDetailUri(@object)?
                .BindParameters(request);

            return new ResponseMovedTemporarily(target);
        }

        /// <summary>
        /// Surfaces a refused state change as a toast, because the redirect would otherwise
        /// return the user to an unchanged page with no explanation. A change that went
        /// through stays silent here: it is visible on the page the redirect lands on, and
        /// stamping the object already raises the "object updated" toast, so reporting it a
        /// second time would only stack notifications. A no-op change is not worth a toast.
        /// </summary>
        /// <param name="result">The outcome reported by the workflow manager.</param>
        private static void Report(WorkflowTransitionResult result)
        {
            if (result is null || result.Succeeded || result.Outcome == WorkflowTransitionOutcome.Unchanged)
            {
                return;
            }

            CoreHub.AddNotification
            (
                "kleenestar.core:notification.title.error",
                Describe(result),
                5000
            );
        }

        /// <summary>
        /// Builds the sentence a refused state change is reported with. A move a relation
        /// refused names what has to happen first, because "not allowed" would leave the user
        /// looking for a workflow rule that is not the reason.
        /// </summary>
        /// <remarks>
        /// The message is composed here rather than by the manager: it is translated and filled
        /// in one step, and the notification pipeline translates a key it is given while passing
        /// finished prose through unchanged.
        /// </remarks>
        /// <param name="result">The outcome reported by the workflow manager.</param>
        /// <returns>The message key, or the composed sentence.</returns>
        private static string Describe(WorkflowTransitionResult result)
        {
            if (result.Outcome != WorkflowTransitionOutcome.Blocked || result.ValidationErrors is not { Count: > 0 })
            {
                return result.Message;
            }

            return string.Format
            (
                CultureInfo.CurrentCulture,
                I18N.Translate(result.Message),
                string.Join(", ", result.ValidationErrors)
            );
        }
    }
}
