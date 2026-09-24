using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WWW.Api._1_.Assignee._objectkey_
{
    /// <summary>
    /// REST endpoint backing the "assign to me" / "unassign" link on the people card
    /// (<see cref="WebFragment.Object.ObjectPropertyPeopleCardFragment"/>). The URL is
    /// <c>/api/1/assignee/{objectkey}</c>; the <c>{objectkey}</c> URL segment is declared
    /// via <see cref="ObjectKeySegmentAttribute"/> so callers can bind the segment from the
    /// current request's <see cref="ObjectKeyParameter"/>.
    /// </summary>
    /// <remarks>
    /// A <c>GET</c> assigns the object addressed by the URL to the current identity, or
    /// clears the assignment when <c>?clear=1</c> is supplied, and then issues a <c>302</c>
    /// redirect back to the object detail page so a plain navigation link can drive the
    /// change without any client-side scripting. The current identity is resolved through
    /// <see cref="CoreHub.SessionManager"/>, which currently attributes every request to the
    /// seeded admin identity until the WebExpress identity flow exposes the authenticated
    /// user on the request.
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
        /// Handles <c>GET {base}</c>: assigns the object addressed by the URL to the current
        /// identity, or clears the assignment when <c>?clear=1</c> is present, then redirects
        /// to the object detail page.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>A <c>302</c> redirect to the object detail page.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Assign(IRequest request)
        {
            var keyParameter = request?.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is not null && global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(@object, request))
            {
                var clear = string.Equals(request?.GetParameter("clear")?.Value, "1", StringComparison.OrdinalIgnoreCase);

                @object.AssigneeId = clear ? null : CoreHub.SessionManager.GetCurrentIdentityId(request);
                @object.Updated = DateTime.UtcNow;

                CoreHub.ObjectManager.Update(@object);
            }

            // dispatch to the detail view matching the object's kind (/issue, /document, …)
            var target = global::KleeneStar.Core.WebFragment.Object.ObjectKindCatalog
                .ResolveDetailUri(@object)?
                .BindParameters(request);

            return new ResponseMovedTemporarily(target);
        }
    }
}
