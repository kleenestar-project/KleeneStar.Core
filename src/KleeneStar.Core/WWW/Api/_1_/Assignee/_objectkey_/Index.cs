using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WWW.Api._1_.Assignee._objectkey_
{
    /// <summary>
    /// REST endpoint behind the "assign to me" / "unassign" action of the people card
    /// (<see cref="ObjectPropertyPeopleCardFragment"/>). The URL is
    /// <c>/api/1/assignee/{objectkey}</c>; <c>?clear=1</c> clears the assignment, otherwise the
    /// object is assigned to the caller.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>PUT</c> changes the assignment and answers <c>{ html }</c>, the assignee row of
    ///   the people card rendered anew; <c>Assets/js/assignee.js</c> puts it in place of the old
    ///   one, so the page is not reloaded. Nobody signed in is <c>403</c>, an object the caller
    ///   cannot see is <c>404</c>, one they may not change <c>403</c>.</item>
    ///   <item><c>GET</c> changes nothing and redirects to the object's detail page, so a link
    ///   opened in a new tab or followed from another site cannot change an assignment.</item>
    /// </list>
    /// </remarks>
    [Title("kleenestar.core:object.assignee.api.title")]
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
        /// Handles <c>GET {base}</c>: redirects to the object detail page without changing the
        /// assignment.
        /// </summary>
        /// <remarks>
        /// A GET travels with the session cookie on any top-level navigation, a link in a mail or
        /// on another site included, so it must not write.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>A <c>302</c> redirect to the object detail page, or <c>404</c>.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Assign(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            // dispatch to the detail view matching the object's kind (/issue, /document, …)
            var target = CoreHub.ObjectKindManager
                .ResolveDetailUri(@object)?
                .BindParameters(request);

            return target is null
                ? new ResponseNotFound()
                : new ResponseMovedTemporarily(target);
        }

        /// <summary>
        /// Handles <c>PUT {base}</c>: changes the assignment and answers the assignee row of the
        /// people card.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><c>{ html }</c>, or <c>403</c> / <c>404</c>.</returns>
        [Method(RequestMethod.PUT)]
        public IResponse Update(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            if (!Change(@object, request))
            {
                return new ResponseForbidden();
            }

            var renderContext = new RenderControlFormContext(null, null, request as Request, null);
            var row = ObjectPropertyPeopleCardFragment.BuildAssignee(@object, request);

            return JsonApi.Json(new { html = row.Render(renderContext, null)?.ToString() ?? string.Empty });
        }

        /// <summary>
        /// Assigns the object to the caller, or clears the assignment for <c>?clear=1</c>, when
        /// a signed-in caller may change the object.
        /// </summary>
        /// <param name="object">The object, or <see langword="null"/>.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the assignment was written.</returns>
        private static bool Change(Model.Entities.Object @object, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (@object is null || identityId == Guid.Empty || !ContentAuthorization.MayWrite(@object, request))
            {
                return false;
            }

            var clear = string.Equals(request?.GetParameter("clear")?.Value, "1", StringComparison.OrdinalIgnoreCase);

            @object.AssigneeId = clear ? null : identityId;
            @object.Updated = DateTime.UtcNow;

            CoreHub.ObjectManager.Update(@object);

            return true;
        }
    }
}
