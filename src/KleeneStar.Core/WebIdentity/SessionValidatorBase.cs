using WebExpress.WebCore.WebIdentity;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// Confirms on every authenticated request that the credential may still act, through
    /// <see cref="WebManager.ISessionManager.Accepts"/>: the stored account is active, the grant of
    /// a sign-in is not revoked, and a personal access token is an active row whose scope allows
    /// the request.
    /// </summary>
    /// <remarks>
    /// WebExpress discovers a <c>public sealed</c> <see cref="IIdentityValidator"/> per plugin
    /// assembly for the applications that plugin declares, so every application derives a sealed
    /// class of its own: <see cref="SessionValidator"/> for the core,
    /// <c>KleeneStar.Portal.WebIdentity.PortalSessionValidator</c> for the portal. A refusal makes
    /// the request anonymous and stops the renewal of the sign-in.
    /// </remarks>
    public abstract class SessionValidatorBase : IIdentityValidator
    {
        /// <summary>
        /// Decides whether the identity of a request is still accepted.
        /// </summary>
        /// <param name="context">The identity, its token's grant and the request.</param>
        /// <returns><see langword="true"/> to accept the identity.</returns>
        public bool Validate(IdentityValidationContext context)
        {
            var sessionManager = CoreHub.SessionManager;

            // without the core hub nothing can be judged; the framework identity stands
            return sessionManager is null || sessionManager.Accepts(SessionCredential.From(context), context?.Request);
        }
    }

    /// <summary>
    /// The session validator of the core application (<see cref="SessionValidatorBase"/>).
    /// </summary>
    public sealed class SessionValidator : SessionValidatorBase
    {
    }
}
