using System;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// Holds when somebody is signed in, as KleeneStar reads the request.
    /// </summary>
    /// <remarks>
    /// Stands in for WebExpress's <c>ConditionLogin</c>, which asks the framework and so
    /// disagrees with everything else on the page in exactly the cases that matter: on a portal
    /// page the framework does not accept the core's token and reads nobody (no sign-out link
    /// for a signed-in user), and it does not know about a locked account or a revoked session
    /// (a sign-out link and a profile for somebody the page treats as anonymous). This one asks
    /// <see cref="WebManager.ISessionManager.GetCurrentIdentityId"/>, the one answer to "who is
    /// this".
    /// </remarks>
    public sealed class SignedInCondition : ICondition
    {
        /// <summary>
        /// Determines whether somebody is signed in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the request resolves to an account.</returns>
        public bool Fulfillment(IRequest request)
        {
            return CoreHub.SessionManager?.GetCurrentIdentityId(request) is { } id && id != Guid.Empty;
        }
    }

    /// <summary>
    /// Holds when nobody is signed in, as KleeneStar reads the request - the exact complement of
    /// <see cref="SignedInCondition"/>, so a page shows the sign-in or the sign-out, never both
    /// and never neither.
    /// </summary>
    public sealed class SignedOutCondition : ICondition
    {
        /// <summary>
        /// Determines whether nobody is signed in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the request resolves to nobody.</returns>
        public bool Fulfillment(IRequest request)
        {
            return !new SignedInCondition().Fulfillment(request);
        }
    }
}
