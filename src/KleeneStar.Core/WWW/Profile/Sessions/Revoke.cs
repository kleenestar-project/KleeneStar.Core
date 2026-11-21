using KleeneStar.Core.WebManager;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WWW.Profile.Sessions
{
    /// <summary>
    /// Ends the session named by the <c>id</c> query parameter and returns to the list. The URL
    /// is <c>/profile/sessions/revoke?id={guid}</c>.
    /// </summary>
    /// <remarks>
    /// The button that leads here already says what it does and which device it refers to, so a
    /// single navigating link is enough: opening the page ends the session and the redirect
    /// re-renders the list without it. A session that does not belong to the caller, and the
    /// session the caller is looking at the page with, are both left alone by
    /// <see cref="IIdentitySessionManager.Revoke"/>.
    /// </remarks>
    [Scope<IScopeGeneral>]
    public sealed class Revoke : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IIdentitySessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="sessionManager">
        /// The manager used to resolve and end the addressed session. Cannot be null.
        /// </param>
        public Revoke(IIdentitySessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Processing of the resource: ends the addressed session, then redirects to the list.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var id = renderContext?.Request?.GetParameter<ParameterId>()?.Value;
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request);

            if (Guid.TryParse(id, out var sessionId))
            {
                var session = _sessionManager.GetSession(sessionId);

                // a caller may only end their own logins
                if (session is not null && session.OwnerId == ownerId)
                {
                    _sessionManager.Revoke(sessionId);
                }
            }

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
